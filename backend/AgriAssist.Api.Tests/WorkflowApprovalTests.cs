using System.Text.Json;
using System.Text.Json.Nodes;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.TaskApproval;
using AgriAssist.Api.ExternalServices.AgenticAI;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Resources;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Models.TaskApproval;
using AgriAssist.Api.Services.Resources;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Services.TaskApproval;
using AgriAssist.Api.Validators.Resources;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class WorkflowApprovalTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Generate_persists_valid_candidate_for_human_approval()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db);
        var service = NewService(db, data.Approver.Id, Candidate);

        var review = await service.GenerateCandidateAsync(data.Workflow.Id, CancellationToken.None);

        Assert.Equal(AgentWorkflowStatus.PendingOfficerApproval, review.Workflow.Status);
        Assert.True(review.Workflow.Version > 1);
        Assert.Contains(review.Validations, item => item.ValidatorName == "SchedulingCandidateValidator" && item.IsValid);
        Assert.Empty(db.FarmTasks);
        Assert.Empty(db.IrrigationSchedules);
        Assert.Empty(db.ResourceReservations);
    }

    [Fact]
    public async Task Generate_sends_verified_stage_profile_pinned_to_member_three_source()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db);
        var stage = new CropStageReference
        {
            StageName = "Planting",
            Sequence = 1,
            TypicalMinDays = 3,
            TypicalMaxDays = 5,
            SourceName = "Field guide"
        };
        var profile = new CropReferenceProfile
        {
            CropTypeId = data.Request.CropTypeId,
            SourceName = "Field guide",
            SourceVersion = "2026-1",
            VerifiedAt = DateTime.UtcNow.AddDays(-1),
            IsActive = true,
            Stages = [stage]
        };
        db.CropReferenceProfiles.Add(profile);
        var weatherStep = data.Workflow.Steps.Single(item => item.AgentName == "WeatherResourceAgent");
        weatherStep.OutputJson = JsonSerializer.Serialize(new
        {
            workflowId = data.Workflow.Id,
            status = "Analyzed",
            weatherRisk = "Medium",
            requirementSource = new { cropReferenceProfileId = profile.Id }
        }, JsonOptions);
        await db.SaveChangesAsync();

        SchedulingValidationInput? sent = null;
        var service = NewService(db, data.Approver.Id, input =>
        {
            sent = input;
            return Candidate(input);
        });
        await service.GenerateCandidateAsync(data.Workflow.Id, CancellationToken.None);

        Assert.NotNull(sent);
        var payload = JsonSerializer.SerializeToElement(sent, JsonOptions);
        Assert.True(payload.TryGetProperty("evidence", out var evidence));
        Assert.Equal(profile.Id, evidence.GetProperty("profileId").GetGuid());
        Assert.Equal("2026-1", evidence.GetProperty("sourceVersion").GetString());
        Assert.Equal(stage.Id, evidence.GetProperty("stages")[0].GetProperty("id").GetGuid());
        Assert.Equal(3, evidence.GetProperty("stages")[0].GetProperty("typicalMinDays").GetInt32());
    }

    [Fact]
    public async Task Generate_does_not_substitute_another_profile_for_an_invalid_member_three_source()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db);
        db.CropReferenceProfiles.Add(new CropReferenceProfile
        {
            CropTypeId = data.Request.CropTypeId,
            SourceName = "Unrelated guide",
            SourceVersion = "v1",
            VerifiedAt = DateTime.UtcNow.AddDays(-1),
            Stages = [new CropStageReference { StageName = "Planting", Sequence = 1, SourceName = "Unrelated guide" }]
        });
        var weatherStep = data.Workflow.Steps.Single(item => item.AgentName == "WeatherResourceAgent");
        weatherStep.OutputJson = JsonSerializer.Serialize(new
        {
            workflowId = data.Workflow.Id,
            status = "Analyzed",
            weatherRisk = "Medium",
            requirementSource = new { cropReferenceProfileId = "invalid-profile-id" }
        }, JsonOptions);
        await db.SaveChangesAsync();

        SchedulingValidationInput? sent = null;
        var service = NewService(db, data.Approver.Id, input =>
        {
            sent = input;
            return Candidate(input);
        });
        await service.GenerateCandidateAsync(data.Workflow.Id, CancellationToken.None);

        Assert.NotNull(sent);
        Assert.Null(sent.Evidence?.ProfileId);
        Assert.Empty(sent.Evidence!.Stages);
    }

    [Fact]
    public async Task Missing_dependency_never_enters_the_approval_queue()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, weatherCompleted: false);
        var service = NewService(db, data.Approver.Id, input => Missing(input, "Weather/resource output is missing."));

        var review = await service.GenerateCandidateAsync(data.Workflow.Id, CancellationToken.None);

        Assert.Equal(AgentWorkflowStatus.MissingDependency, review.Workflow.Status);
        Assert.DoesNotContain(review.Validations, item => item.IsValid);
        Assert.Empty(db.FarmTasks);
        Assert.Empty(db.IrrigationSchedules);
    }

    [Fact]
    public async Task Blocked_proposal_is_reviewable_but_cannot_be_approved()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db);
        var service = NewService(db, data.Approver.Id, input => new SchedulingValidationOutput(
            input.WorkflowId, input.CandidateRevision, "CandidateBlocked", true, false,
            ["Verified inventory is insufficient."], [], [], [], null,
            [new SchedulingConstraint("RESOURCE_SHORTAGE", "Blocking", "Verified inventory is insufficient.")], 2));

        var review = await service.GenerateCandidateAsync(data.Workflow.Id, CancellationToken.None);

        Assert.Equal(AgentWorkflowStatus.CandidateBlocked, review.Workflow.Status);
        Assert.Contains(review.Steps, item => item.AgentName == "SchedulingValidationAgent"
            && item.Output.GetProperty("status").GetString() == "CandidateBlocked");
        Assert.Empty(db.FarmTasks);
        Assert.Empty(db.IrrigationSchedules);
        Assert.Empty(db.ResourceReservations);
        var ex = await Assert.ThrowsAsync<ApiException>(() => service.ApproveAsync(data.Workflow.Id,
            new WorkflowDecisionRequest(review.Workflow.CandidateRevision, review.Workflow.Version, "blocked", ""),
            CancellationToken.None));
        Assert.Equal("WORKFLOW_DECISION_NOT_ALLOWED", ex.Code);
    }

    [Fact]
    public async Task Version_two_ready_candidate_without_verified_profile_cannot_enter_approval()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db);
        var service = NewService(db, data.Approver.Id, input => Candidate(input) with
        {
            ContractVersion = 2,
            CandidateIrrigation = []
        });

        var review = await service.GenerateCandidateAsync(data.Workflow.Id, CancellationToken.None);

        Assert.NotEqual(AgentWorkflowStatus.PendingOfficerApproval, review.Workflow.Status);
        Assert.Contains(review.Validations.SelectMany(item => item.Errors),
            item => item.Contains("verified", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(db.FarmTasks);
        Assert.Empty(db.IrrigationSchedules);
    }

    [Fact]
    public async Task Version_two_ready_candidate_can_have_zero_irrigation_without_a_rule()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, includeStock: true);
        var profile = await AddVersionTwoEvidenceAsync(db, data);
        var service = NewService(db, data.Approver.Id, input => VersionTwoCandidate(input, data.Stock!.Id, profile.Rules.Single().Id));

        var review = await service.GenerateCandidateAsync(data.Workflow.Id, CancellationToken.None);

        Assert.Equal(AgentWorkflowStatus.PendingOfficerApproval, review.Workflow.Status);
        Assert.DoesNotContain(review.Validations.SelectMany(item => item.Errors), item => item.Length > 0);
        Assert.Empty(db.IrrigationSchedules);
        Assert.Empty(db.FarmTasks);
        Assert.Empty(db.ResourceReservations);
    }

    [Fact]
    public async Task Version_two_candidate_rejects_a_fabricated_stage_explanation()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, includeStock: true);
        var profile = await AddVersionTwoEvidenceAsync(db, data);
        var service = NewService(db, data.Approver.Id, input =>
        {
            var candidate = VersionTwoCandidate(input, data.Stock!.Id, profile.Rules.Single().Id);
            return candidate with
            {
                CandidateTasks = [candidate.CandidateTasks[0] with { Reason = "The weather report confirms planting is safe." }]
            };
        });

        var review = await service.GenerateCandidateAsync(data.Workflow.Id, CancellationToken.None);

        Assert.NotEqual(AgentWorkflowStatus.PendingOfficerApproval, review.Workflow.Status);
        Assert.Contains(review.Validations.SelectMany(item => item.Errors),
            item => item.Contains("explanation", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(db.FarmTasks);
        Assert.Empty(db.ResourceReservations);
    }

    [Fact]
    public async Task Version_two_candidate_accepts_a_long_verified_irrigation_rule_key()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, includeStock: true);
        var longRuleKey = new string('r', 120);
        var profile = await AddVersionTwoEvidenceAsync(db, data, irrigationRuleKey: longRuleKey);
        var irrigationRule = profile.Rules.Single(item => item.RuleType == "IrrigationSchedule");
        var service = NewService(db, data.Approver.Id, input =>
        {
            var candidate = VersionTwoCandidate(input, data.Stock!.Id, profile.Rules.Single(item => item.RuleType == "ResourceRequirement").Id);
            return candidate with
            {
                CandidateIrrigation = [new SchedulingCandidateIrrigation(
                    input.FieldId!.Value,
                    StartAt(input.PreferredStartDate.AddDays(1), 6),
                    30,
                    "Candidate only; officer approval is required.",
                    $"Verified irrigation rule {irrigationRule.RuleKey[..100]} specifies this offset, UTC time and duration.",
                    [new SchedulingSource("IrrigationRule", irrigationRule.Id, "Verified guide", profile.Id,
                        profile.SourceVersion, irrigationRule.VerifiedAt)])]
            };
        });

        var review = await service.GenerateCandidateAsync(data.Workflow.Id, CancellationToken.None);

        Assert.Equal(AgentWorkflowStatus.PendingOfficerApproval, review.Workflow.Status);
        Assert.DoesNotContain(review.Validations.SelectMany(item => item.Errors), item => item.Length > 0);
    }

    [Fact]
    public async Task Malformed_member_three_stock_id_fails_validation_without_throwing()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, includeStock: true);
        var profile = await AddVersionTwoEvidenceAsync(db, data);
        var weatherStep = data.Workflow.Steps.Single(item => item.AgentName == "WeatherResourceAgent");
        var weather = JsonNode.Parse(weatherStep.OutputJson)!;
        weather["resourceChecks"]![0]!["inventoryStockId"] = 42;
        weatherStep.OutputJson = weather.ToJsonString();
        await db.SaveChangesAsync();

        var service = NewService(db, data.Approver.Id,
            input => VersionTwoCandidate(input, data.Stock!.Id, profile.Rules.Single().Id));
        var review = await service.GenerateCandidateAsync(data.Workflow.Id, CancellationToken.None);

        Assert.NotEqual(AgentWorkflowStatus.PendingOfficerApproval, review.Workflow.Status);
        Assert.Contains(review.Validations.SelectMany(item => item.Errors),
            item => item.Contains("stock mapping", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(db.ResourceReservations);
    }

    [Fact]
    public async Task Deactivated_profile_rejects_version_two_approval_without_final_writes()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, includeStock: true);
        var profile = await AddVersionTwoEvidenceAsync(db, data);
        var service = NewService(db, data.Approver.Id, input => VersionTwoCandidate(input, data.Stock!.Id, profile.Rules.Single().Id));
        var review = await service.GenerateCandidateAsync(data.Workflow.Id, CancellationToken.None);
        Assert.Equal(AgentWorkflowStatus.PendingOfficerApproval, review.Workflow.Status);

        profile.IsActive = false;
        await db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<ApiException>(() => service.ApproveAsync(data.Workflow.Id,
            new WorkflowDecisionRequest(review.Workflow.CandidateRevision, review.Workflow.Version, "profile-drift", ""),
            CancellationToken.None));

        Assert.Equal("WORKFLOW_REVALIDATION_FAILED", ex.Code);
        Assert.Empty(db.FarmTasks);
        Assert.Empty(db.IrrigationSchedules);
        Assert.Empty(db.ResourceReservations);
    }

    [PostgreSqlFact]
    public async Task PostgreSql_competing_approvals_have_one_winner_and_one_final_write_set()
    {
        var connectionString = Environment.GetEnvironmentVariable("AGRIASSIST_TEST_POSTGRES_CONNECTION_STRING")!;
        Guid workflowId;
        Guid approverId;
        WorkflowDecisionRequest request;
        await using (var setup = NewPostgreSqlDbContext(connectionString))
        {
            await setup.Database.MigrateAsync();
            var data = await SeedAsync(setup);
            var review = await NewService(setup, data.Approver.Id, Candidate)
                .GenerateCandidateAsync(data.Workflow.Id, CancellationToken.None);
            Assert.Equal(AgentWorkflowStatus.PendingOfficerApproval, review.Workflow.Status);
            workflowId = data.Workflow.Id;
            approverId = data.Approver.Id;
            request = new WorkflowDecisionRequest(review.Workflow.CandidateRevision,
                review.Workflow.Version, "concurrent-approval-" + Guid.NewGuid().ToString("N"), "Approved.");
        }

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = Enumerable.Range(0, 2).Select(async index =>
        {
            await gate.Task;
            await using var db = NewPostgreSqlDbContext(connectionString);
            try
            {
                await NewService(db, approverId, Candidate).ApproveAsync(workflowId,
                    request with { IdempotencyKey = $"{request.IdempotencyKey}-{index}" }, CancellationToken.None);
                return "APPROVED";
            }
            catch (ApiException ex) { return ex.Code; }
        }).ToArray();
        gate.SetResult();
        var results = await Task.WhenAll(attempts);

        await using var verification = NewPostgreSqlDbContext(connectionString);
        Assert.Single(results.Where(item => item == "APPROVED"));
        Assert.Contains(results, item => item is "WORKFLOW_CHANGED" or "WORKFLOW_STALE" or "WORKFLOW_DECISION_CONFLICT");
        Assert.Equal(1, await verification.FarmTasks.CountAsync(item => item.GeneratedByWorkflowId == workflowId));
        Assert.Equal(1, await verification.IrrigationSchedules.CountAsync(item => item.GeneratedByWorkflowId == workflowId));
        Assert.Equal(1, await verification.ApprovalDecisions.CountAsync(item => item.AgentWorkflowId == workflowId));
    }

    [PostgreSqlFact]
    public async Task PostgreSql_reservation_insert_failure_rolls_back_approval_work()
    {
        var connectionString = Environment.GetEnvironmentVariable("AGRIASSIST_TEST_POSTGRES_CONNECTION_STRING")!;
        Guid workflowId;
        Guid approverId;
        Guid stockId;
        WorkflowDecisionRequest request;
        await using (var setup = NewPostgreSqlDbContext(connectionString))
        {
            await setup.Database.MigrateAsync();
            var data = await SeedAsync(setup, includeStock: true);
            var profile = await AddVersionTwoEvidenceAsync(setup, data);
            var review = await NewService(setup, data.Approver.Id,
                input => VersionTwoCandidate(input, data.Stock!.Id, profile.Rules.Single().Id))
                .GenerateCandidateAsync(data.Workflow.Id, CancellationToken.None);
            Assert.Equal(AgentWorkflowStatus.PendingOfficerApproval, review.Workflow.Status);
            workflowId = data.Workflow.Id;
            approverId = data.Approver.Id;
            stockId = data.Stock!.Id;
            request = new WorkflowDecisionRequest(review.Workflow.CandidateRevision, review.Workflow.Version,
                "forced-rollback-" + Guid.NewGuid().ToString("N"), "Approved.");
        }

        await using (var triggerDb = NewPostgreSqlDbContext(connectionString))
        {
            await triggerDb.Database.ExecuteSqlRawAsync("""
                CREATE OR REPLACE FUNCTION member4_reject_reservation_insert() RETURNS trigger AS $$
                BEGIN RAISE EXCEPTION 'member4 intentional reservation failure'; END;
                $$ LANGUAGE plpgsql;
                """);
            await triggerDb.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER member4_reject_reservation_insert
                BEFORE INSERT ON "ResourceReservations"
                FOR EACH ROW EXECUTE FUNCTION member4_reject_reservation_insert();
                """);
        }

        try
        {
            await using var approvalDb = NewPostgreSqlDbContext(connectionString);
            var exception = await Assert.ThrowsAsync<ApiException>(() =>
                NewService(approvalDb, approverId, Candidate).ApproveAsync(workflowId, request, CancellationToken.None));
            Assert.Equal("WORKFLOW_DECISION_CONFLICT", exception.Code);
            await using var verification = NewPostgreSqlDbContext(connectionString);
            Assert.Equal(AgentWorkflowStatus.PendingOfficerApproval,
                (await verification.AgentWorkflows.SingleAsync(item => item.Id == workflowId)).Status);
            Assert.Empty(await verification.FarmTasks.Where(item => item.GeneratedByWorkflowId == workflowId).ToListAsync());
            Assert.Empty(await verification.IrrigationSchedules.Where(item => item.GeneratedByWorkflowId == workflowId).ToListAsync());
            Assert.Empty(await verification.ResourceReservations.Where(item => item.GeneratedByWorkflowId == workflowId).ToListAsync());
            Assert.Empty(await verification.ApprovalDecisions.Where(item => item.AgentWorkflowId == workflowId).ToListAsync());
            Assert.Equal(0m, (await verification.InventoryStocks.SingleAsync(item => item.Id == stockId)).ReservedQuantity);
        }
        finally
        {
            await using var cleanup = NewPostgreSqlDbContext(connectionString);
            await cleanup.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS member4_reject_reservation_insert ON \"ResourceReservations\";");
            await cleanup.Database.ExecuteSqlRawAsync("DROP FUNCTION IF EXISTS member4_reject_reservation_insert();");
        }
    }

    [PostgreSqlFact]
    public async Task PostgreSql_version_two_approval_preserves_three_decimal_reservation_quantity()
    {
        var connectionString = Environment.GetEnvironmentVariable("AGRIASSIST_TEST_POSTGRES_CONNECTION_STRING")!;
        Guid workflowId;
        Guid stockId;
        await using (var setup = NewPostgreSqlDbContext(connectionString))
        {
            await setup.Database.MigrateAsync();
            var data = await SeedAsync(setup, includeStock: true);
            var profile = await AddVersionTwoEvidenceAsync(setup, data, 1.234m);
            var service = NewService(setup, data.Approver.Id,
                input => VersionTwoCandidate(input, data.Stock!.Id, profile.Rules.Single().Id, 1.234m));
            var review = await service.GenerateCandidateAsync(data.Workflow.Id, CancellationToken.None);
            Assert.Equal(AgentWorkflowStatus.PendingOfficerApproval, review.Workflow.Status);
            await service.ApproveAsync(data.Workflow.Id,
                new WorkflowDecisionRequest(review.Workflow.CandidateRevision, review.Workflow.Version,
                    "three-decimal-" + Guid.NewGuid().ToString("N"), "Approved."), CancellationToken.None);
            workflowId = data.Workflow.Id;
            stockId = data.Stock!.Id;
        }

        await using var verification = NewPostgreSqlDbContext(connectionString);
        Assert.Equal(1.234m, (await verification.ResourceReservations.SingleAsync(item =>
            item.GeneratedByWorkflowId == workflowId)).Quantity);
        Assert.Equal(1.234m, (await verification.InventoryStocks.SingleAsync(item => item.Id == stockId)).ReservedQuantity);
        Assert.Equal(1.234m, await verification.StockTransactions.Where(item =>
            item.InventoryStockId == stockId && item.Type == StockTransactionType.Reserve).SumAsync(item => item.Quantity));
    }

    [Fact]
    public async Task Approve_atomically_creates_final_work_and_is_idempotent()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db);
        var service = NewService(db, data.Approver.Id, Candidate);
        var review = await service.GenerateCandidateAsync(data.Workflow.Id, CancellationToken.None);
        var request = new WorkflowDecisionRequest(review.Workflow.CandidateRevision, review.Workflow.Version, "approve-1", "Approved after evidence review.");

        var first = await service.ApproveAsync(data.Workflow.Id, request, CancellationToken.None);
        var replay = await service.ApproveAsync(data.Workflow.Id, request, CancellationToken.None);

        Assert.Equal(first.DecisionId, replay.DecisionId);
        Assert.Equal(AgentWorkflowStatus.Completed, first.Status);
        Assert.Single(db.FarmTasks);
        Assert.Single(db.IrrigationSchedules);
        Assert.Single(db.ApprovalDecisions);
        Assert.All(db.FarmTasks, item => Assert.Equal(data.Workflow.Id, item.GeneratedByWorkflowId));
        Assert.Equal(CropPlanRequestStatus.Approved, (await db.CropPlanRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task Stale_and_competing_decisions_do_not_duplicate_final_records()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db);
        var service = NewService(db, data.Approver.Id, Candidate);
        var review = await service.GenerateCandidateAsync(data.Workflow.Id, CancellationToken.None);
        var approved = new WorkflowDecisionRequest(review.Workflow.CandidateRevision, review.Workflow.Version, "winner", "Approved.");
        await service.ApproveAsync(data.Workflow.Id, approved, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ApiException>(() => service.RejectAsync(
            data.Workflow.Id,
            new WorkflowDecisionRequest(review.Workflow.CandidateRevision, review.Workflow.Version, "loser", "Reject instead."),
            CancellationToken.None));

        Assert.Equal("WORKFLOW_STALE", exception.Code);
        Assert.Single(db.FarmTasks);
        Assert.Single(db.IrrigationSchedules);
        Assert.Single(db.ApprovalDecisions);
    }

    [Fact]
    public async Task Rejection_records_reason_without_creating_final_work()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db);
        var service = NewService(db, data.Approver.Id, Candidate);
        var review = await service.GenerateCandidateAsync(data.Workflow.Id, CancellationToken.None);

        var result = await service.RejectAsync(data.Workflow.Id,
            new WorkflowDecisionRequest(review.Workflow.CandidateRevision, review.Workflow.Version, "reject-1", "Weather risk is too high."),
            CancellationToken.None);

        Assert.Equal(AgentWorkflowStatus.Rejected, result.Status);
        Assert.Empty(db.FarmTasks);
        Assert.Empty(db.IrrigationSchedules);
        Assert.Equal("Weather risk is too high.", (await db.ApprovalDecisions.SingleAsync()).Comment);
    }

    [Fact]
    public async Task Revision_increments_revision_and_invalidates_scheduling_output()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db);
        var service = NewService(db, data.Approver.Id, Candidate);
        var review = await service.GenerateCandidateAsync(data.Workflow.Id, CancellationToken.None);

        var result = await service.RequestRevisionAsync(data.Workflow.Id,
            new WorkflowDecisionRequest(review.Workflow.CandidateRevision, review.Workflow.Version, "revision-1", "Move irrigation to a later date."),
            CancellationToken.None);

        Assert.Equal(AgentWorkflowStatus.RevisionRequested, result.Status);
        Assert.Equal(2, result.CandidateRevision);
        var step = await db.AgentSteps.SingleAsync(item => item.AgentName == "SchedulingValidationAgent");
        Assert.Equal(AgentStepStatus.Pending, step.Status);
        Assert.Equal("{}", step.OutputJson);
        Assert.Empty(db.FarmTasks);
        Assert.Empty(db.IrrigationSchedules);
    }

    [Fact]
    public async Task Revision_limit_is_enforced_server_side()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db);
        var service = NewService(db, data.Approver.Id, Candidate);

        for (var revision = 1; revision <= 3; revision++)
        {
            var review = await service.GenerateCandidateAsync(data.Workflow.Id, CancellationToken.None);
            await service.RequestRevisionAsync(data.Workflow.Id,
                new WorkflowDecisionRequest(review.Workflow.CandidateRevision, review.Workflow.Version, $"revision-{revision}", "Adjust the proposed schedule."),
                CancellationToken.None);
        }

        var finalReview = await service.GenerateCandidateAsync(data.Workflow.Id, CancellationToken.None);
        var exception = await Assert.ThrowsAsync<ApiException>(() => service.RequestRevisionAsync(data.Workflow.Id,
            new WorkflowDecisionRequest(finalReview.Workflow.CandidateRevision, finalReview.Workflow.Version, "revision-4", "Another change."),
            CancellationToken.None));

        Assert.Equal("REVISION_LIMIT_REACHED", exception.Code);
        Assert.Equal(3, (await db.AgentWorkflows.SingleAsync()).RevisionCount);
    }

    [Fact]
    public async Task Conflict_validation_blocks_candidate_before_decision()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db);
        var dueAt = StartAt(data.Request.PreferredStartDate, 8);
        db.FarmTasks.Add(new FarmTask
        {
            FarmId = data.Farm.Id,
            AssignedToUserId = data.Farmer.Id,
            DueAt = dueAt,
            Title = "Existing task",
            Status = FarmTaskStatus.PendingApproval
        });
        await db.SaveChangesAsync();
        var service = NewService(db, data.Approver.Id, Candidate);

        var review = await service.GenerateCandidateAsync(data.Workflow.Id, CancellationToken.None);

        Assert.Equal(AgentWorkflowStatus.Failed, review.Workflow.Status);
        Assert.Contains(review.Validations.SelectMany(item => item.Errors), item => item.Contains("conflicts"));
    }

    [Fact]
    public async Task Revalidation_failure_leaves_no_partial_final_records()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, includeStock: true);
        var service = NewService(db, data.Approver.Id, input => Candidate(input, data.Stock!.Id, 2));
        var review = await service.GenerateCandidateAsync(data.Workflow.Id, CancellationToken.None);
        data.Stock!.ReservedQuantity = data.Stock.QuantityOnHand;
        data.Stock.RowVersion = Guid.NewGuid().ToByteArray();
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<ApiException>(() => service.ApproveAsync(data.Workflow.Id,
            new WorkflowDecisionRequest(review.Workflow.CandidateRevision, review.Workflow.Version, "stock-moved", "Approve."),
            CancellationToken.None));

        Assert.Equal("WORKFLOW_REVALIDATION_FAILED", exception.Code);
        Assert.Empty(db.FarmTasks);
        Assert.Empty(db.IrrigationSchedules);
        Assert.Empty(db.ResourceReservations);
        Assert.Empty(db.ApprovalDecisions);
        Assert.Equal(AgentWorkflowStatus.PendingOfficerApproval, (await db.AgentWorkflows.SingleAsync()).Status);
    }

    private static WorkflowApprovalService NewService(
        AppDbContext db,
        Guid userId,
        Func<SchedulingValidationInput, SchedulingValidationOutput> response)
    {
        var currentUser = new FixedCurrentUserService(ApplicationRole.AgriculturalOfficer, userId);
        var resources = new ResourceService(
            db,
            currentUser,
            new ResourceCategoryRequestValidator(),
            new SupplierRequestValidator(),
            new ResourceRequestValidator(),
            new InventoryStockRequestValidator(),
            new ResourceReservationRequestValidator());
        return new WorkflowApprovalService(db, currentUser, new FakeSchedulingClient(response), resources);
    }

    private static SchedulingValidationOutput Candidate(SchedulingValidationInput input) => Candidate(input, null, 0);

    private static async Task<CropReferenceProfile> AddVersionTwoEvidenceAsync(AppDbContext db, SeededData data,
        decimal requiredQuantity = 2m, string? irrigationRuleKey = null)
    {
        var verified = DateTime.UtcNow.AddDays(-1);
        var rules = new List<CropRuleReference>
        {
            new() { RuleType = "ResourceRequirement", RuleKey = "seed", StructuredValueJson = "{}",
                SourceName = "Verified guide", VerifiedAt = verified }
        };
        if (irrigationRuleKey is not null)
            rules.Add(new CropRuleReference { RuleType = "IrrigationSchedule", RuleKey = irrigationRuleKey,
                StructuredValueJson = "{\"dayOffsetFromPlanting\":1,\"startTimeUtc\":\"06:00\",\"durationMinutes\":30}",
                SourceName = "Verified guide", VerifiedAt = verified });
        var profile = new CropReferenceProfile
        {
            CropTypeId = data.Request.CropTypeId,
            SourceName = "Verified guide",
            SourceVersion = "v1",
            VerifiedAt = verified,
            IsActive = true,
            Stages = [new CropStageReference { StageName = "Planting", Sequence = 1,
                TypicalMinDays = 3, TypicalMaxDays = 5, SourceName = "Verified guide" }],
            Rules = rules
        };
        db.CropReferenceProfiles.Add(profile);
        var weatherStep = data.Workflow.Steps.Single(item => item.AgentName == "WeatherResourceAgent");
        weatherStep.OutputJson = JsonSerializer.Serialize(new
        {
            workflowId = data.Workflow.Id,
            status = "Analyzed",
            weatherRisk = "Medium",
            requirementStatus = "Sufficient",
            requirementSource = new { cropReferenceProfileId = profile.Id },
            resourceRequirements = new[] { new { ruleId = profile.Rules.Single(item => item.RuleType == "ResourceRequirement").Id,
                resourceId = data.Stock!.ResourceId, resourceName = "Seed", unit = "kg",
                requiredQuantity, sufficient = true, requirementStatus = "Sufficient" } },
            resourceChecks = new[] { new { inventoryStockId = data.Stock!.Id,
                resourceId = data.Stock.ResourceId, resourceName = "Seed", unit = "kg",
                availableQuantity = 5m, requested = requiredQuantity, sufficient = true,
                requirementStatus = "Sufficient" } }
        }, JsonOptions);
        await db.SaveChangesAsync();
        return profile;
    }

    private static SchedulingValidationOutput VersionTwoCandidate(SchedulingValidationInput input, Guid stockId, Guid ruleId,
        decimal quantity = 2m)
    {
        var evidence = input.Evidence!;
        var stage = Assert.Single(evidence.Stages);
        return new SchedulingValidationOutput(input.WorkflowId, input.CandidateRevision, "CandidateReady", true, true,
            ["Officer review is required."],
            [new SchedulingCandidateTask(input.FarmId, "Review Planting stage", "Review verified stage.",
                StartAt(input.PreferredStartDate, 8), input.AssignedToUserId,
                "Verified stage 1 follows the profile's minimum stage durations.",
                [new SchedulingSource("CropStage", stage.Id, "Verified guide", evidence.ProfileId,
                    evidence.SourceVersion, evidence.VerifiedAt)])],
            [],
            [new SchedulingCandidateReservation(stockId, quantity, "Candidate crop-plan resource use", null,
                $"Member 3 verified {quantity} kg required and available.",
                [new SchedulingSource("ResourceRequirement", ruleId, "Seed", evidence.ProfileId,
                    evidence.SourceVersion, evidence.VerifiedAt)])],
            null, [new SchedulingConstraint("HUMAN_APPROVAL", "Blocking", "Officer approval is required.")], 2);
    }

    private static SchedulingValidationOutput Candidate(SchedulingValidationInput input, Guid? stockId, decimal quantity)
    {
        var reservations = stockId.HasValue
            ? new[] { new SchedulingCandidateReservation(stockId.Value, quantity, "Workflow materials", null) }
            : [];
        return new SchedulingValidationOutput(
            input.WorkflowId,
            input.CandidateRevision,
            "CandidateReady",
            true,
            true,
            ["Officer review is required."],
            [new SchedulingCandidateTask(input.FarmId, "Review field readiness", "Review stored evidence.", StartAt(input.PreferredStartDate, 8), input.AssignedToUserId)],
            [new SchedulingCandidateIrrigation(input.FieldId!.Value, StartAt(input.PreferredStartDate.AddDays(1), 6), 60, "Candidate irrigation.")],
            reservations,
            null,
            [new SchedulingConstraint("HUMAN_APPROVAL", "Blocking", "Officer approval is required.")]);
    }

    private static SchedulingValidationOutput Missing(SchedulingValidationInput input, string warning) =>
        new(input.WorkflowId, input.CandidateRevision, "MissingDependency", true, false, [warning], [], [], [], null,
            [new SchedulingConstraint("UPSTREAM_DEPENDENCY", "Blocking", warning)]);

    private static DateTime StartAt(DateOnly date, int hour) =>
        DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(hour, 0)), DateTimeKind.Utc);

    private static AppDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static AppDbContext NewPostgreSqlDbContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);

    private sealed class PostgreSqlFactAttribute : FactAttribute
    {
        public PostgreSqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGRIASSIST_TEST_POSTGRES_CONNECTION_STRING")))
                Skip = "Set AGRIASSIST_TEST_POSTGRES_CONNECTION_STRING to run approval transaction tests.";
        }
    }

    private static async Task<SeededData> SeedAsync(AppDbContext db, bool weatherCompleted = true, bool includeStock = false)
    {
        var farmer = new AppUser { FullName = "Farmer", Email = $"farmer.{Guid.NewGuid()}@example.test", PasswordHash = "hash", Role = ApplicationRole.Farmer, IsActive = true };
        var approver = new AppUser { FullName = "Approver", Email = $"approver.{Guid.NewGuid()}@example.test", PasswordHash = "hash", Role = ApplicationRole.AgriculturalOfficer, IsActive = true };
        var farm = new Farm { Name = "Workflow Farm", Location = "Kurunegala", TotalArea = 10, OwnerUser = farmer };
        var field = new Field { Name = "Field A", Area = 2, SoilType = "Loam", Farm = farm, IsActive = true };
        var crop = new CropType { Name = $"Rice-{Guid.NewGuid()}", IsActive = true };
        var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var request = new CropPlanRequest
        {
            Farm = farm,
            Field = field,
            CropType = crop,
            RequestedByUser = farmer,
            PreferredStartDate = start,
            PreferredEndDate = start.AddDays(7),
            Budget = 12000,
            Objective = "Prepare the next crop cycle.",
            Status = CropPlanRequestStatus.PreliminaryGenerated
        };
        var workflow = new AgentWorkflow
        {
            CropPlanRequest = request,
            InitiatedByUser = farmer,
            Objective = request.Objective,
            Status = AgentWorkflowStatus.Pending,
            CurrentStep = "SchedulingValidationAgent"
        };
        var coordinator = JsonSerializer.Serialize(new { workflowId = workflow.Id, status = "Planned", referenceDataStatus = "Available", warnings = Array.Empty<string>() }, JsonOptions);
        var fieldOutput = JsonSerializer.Serialize(new { workflowId = workflow.Id, status = "Analyzed", priority = "High", warnings = Array.Empty<string>() }, JsonOptions);
        var weather = JsonSerializer.Serialize(new { workflowId = workflow.Id, status = "Analyzed", weatherRisk = "Medium", warnings = Array.Empty<string>() }, JsonOptions);
        workflow.Steps =
        [
            new AgentStep { AgentName = "CropPlanningCoordinatorAgent", StepName = "CropPlanningCoordinator", Sequence = 1, Status = AgentStepStatus.Completed, OutputJson = coordinator },
            new AgentStep { AgentName = "CropFieldAnalysisAgent", StepName = "FieldAnalysis", Sequence = 2, Status = AgentStepStatus.Completed, OutputJson = fieldOutput },
            new AgentStep { AgentName = "WeatherResourceAgent", StepName = "WeatherResourceAnalysis", Sequence = 3, Status = weatherCompleted ? AgentStepStatus.Completed : AgentStepStatus.Pending, OutputJson = weatherCompleted ? weather : "{}" },
            new AgentStep { AgentName = "SchedulingValidationAgent", StepName = "Scheduling", Sequence = 4, Status = AgentStepStatus.Pending }
        ];

        InventoryStock? stock = null;
        if (includeStock)
        {
            stock = new InventoryStock
            {
                Resource = new Resource { Name = "Seed", Unit = "kg", ResourceCategory = new ResourceCategory { Name = $"Seed-{Guid.NewGuid()}" } },
                QuantityOnHand = 5,
                ReservedQuantity = 0,
                LowStockThreshold = 1,
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            db.Add(stock);
        }

        db.AddRange(farmer, approver, farm, field, crop, request, workflow);
        await db.SaveChangesAsync();
        return new SeededData(farmer, approver, farm, field, request, workflow, stock);
    }

    private sealed record SeededData(AppUser Farmer, AppUser Approver, Farm Farm, Field Field, CropPlanRequest Request, AgentWorkflow Workflow, InventoryStock? Stock);

    private sealed class FixedCurrentUserService(ApplicationRole role, Guid userId) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;
        public ApplicationRole? Role { get; } = role;
        public bool IsInRole(ApplicationRole roleToCheck) => Role == roleToCheck;
    }

    private sealed class FakeSchedulingClient(Func<SchedulingValidationInput, SchedulingValidationOutput> response) : ISchedulingValidationAIClient
    {
        public Task<SchedulingValidationOutput> RunSchedulingValidationAsync(SchedulingValidationInput input, CancellationToken cancellationToken) =>
            Task.FromResult(response(input));
    }
}
