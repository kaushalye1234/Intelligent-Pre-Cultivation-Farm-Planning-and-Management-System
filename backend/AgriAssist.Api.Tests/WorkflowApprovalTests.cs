using System.Text.Json;
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
