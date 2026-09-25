using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
using System.Text.Json;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.ExternalServices.AgenticAI;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Inspections;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.CropPlanning;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.CropPlanning;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Tests;

public sealed class CropPlanningAiWorkflowTests
{
    [Fact]
    public async Task Coordinator_receives_persisted_farmer_variety_season_dates_and_history()
    {
        await using var db = NewDbContext();
        var data = await SeedPlanAsync(db, CropPlanRequestStatus.Submitted);
        var variety = new CropVariety { CropTypeId = data.Request.CropTypeId, Name = "Bg 352", IsActive = true };
        var previous = new CropType { Name = "Maize", IsActive = true };
        data.Request.CropVariety = variety;
        data.Request.PreviousCropType = previous;
        data.Request.CultivationSeason = CultivationSeason.Maha;
        data.Request.PreferredEndDate = new DateOnly(2027, 2, 15);
        data.Request.PreviousKnownProblemsJson = JsonSerializer.Serialize(new[] { "PreviousFlooding" });
        db.AddRange(variety, previous);
        await db.SaveChangesAsync();
        var client = new RecordingAiClient();

        await NewService(db, data.Farmer.Id, client).StartAiWorkflowAsync(data.Request.Id, CancellationToken.None);

        Assert.NotNull(client.LastInput);
        Assert.Equal(variety.Id, client.LastInput.CropVarietyId);
        Assert.Equal("Bg 352", client.LastInput.CropVarietyName);
        Assert.Equal("Maha", client.LastInput.CultivationSeason);
        Assert.Equal(new DateOnly(2027, 2, 15), client.LastInput.PreferredEndDate);
        Assert.Equal(previous.Id, client.LastInput.PreviousCropTypeId);
        Assert.Equal("Maize", client.LastInput.PreviousCropTypeName);
        Assert.Equal(["PreviousFlooding"], client.LastInput.PreviousKnownProblems);
        var inputJson = (await db.AgentSteps.SingleAsync(item => item.AgentName == "CropPlanningCoordinatorAgent")).InputJson;
        Assert.Contains("\"cropVarietyName\":\"Bg 352\"", inputJson);
    }

    [Fact]
    public async Task Farmer_request_persists_variety_season_previous_crop_and_historical_problems()
    {
        await using var db = NewDbContext();
        var data = await SeedPlanAsync(db, CropPlanRequestStatus.Cancelled);
        var variety = new CropVariety { CropTypeId = data.Request.CropTypeId, Name = "Bg 352", IsActive = true };
        var previous = new CropType { Name = "Maize", IsActive = true };
        db.AddRange(variety, previous);
        await db.SaveChangesAsync();

        var created = await NewService(db, data.Farmer.Id, new PlannedAiClient()).CreateCropPlanRequestAsync(
            NewRequest(data) with {
                CropVarietyId = variety.Id,
                CultivationSeason = CultivationSeason.Maha,
                PreviousCropTypeId = previous.Id,
                PreviousKnownProblems = ["PreviousFlooding", "PreviousPestIssue"]
            }, CancellationToken.None);

        Assert.Equal(CropPlanRequestStatus.Submitted, created.Status);
        Assert.Equal(variety.Id, created.CropVarietyId);
        Assert.Equal(CultivationSeason.Maha, created.CultivationSeason);
        Assert.Equal(previous.Id, created.PreviousCropTypeId);
        Assert.Equal(["PreviousFlooding", "PreviousPestIssue"], created.PreviousKnownProblems);
        Assert.Equal(created.CropVarietyId, (await db.CropPlanRequests.SingleAsync(item => item.Id == created.Id)).CropVarietyId);
    }

    [Fact]
    public async Task Farmer_request_rejects_other_farm_mismatched_field_and_other_crop_variety()
    {
        await using var db = NewDbContext();
        var data = await SeedPlanAsync(db, CropPlanRequestStatus.Cancelled);
        var service = NewService(db, data.Farmer.Id, new PlannedAiClient());
        var otherFarmerError = await Assert.ThrowsAsync<ApiException>(() =>
            NewService(db, Guid.NewGuid(), new PlannedAiClient()).CreateCropPlanRequestAsync(NewRequest(data), CancellationToken.None));
        Assert.Equal("NOT_FOUND", otherFarmerError.Code);

        var otherFarm = new Farm { Name = "Other", Location = "South", TotalArea = 5, OwnerUserId = data.Farmer.Id };
        var otherField = new Field { Farm = otherFarm, Name = "Other field", Area = 1, SoilType = "Loam", IsActive = true };
        var otherCrop = new CropType { Name = "Tomato", IsActive = true };
        var wrongVariety = new CropVariety { CropType = otherCrop, Name = "Tomato A", IsActive = true };
        db.AddRange(otherFarm, otherField, otherCrop, wrongVariety);
        await db.SaveChangesAsync();

        var fieldError = await Assert.ThrowsAsync<ApiException>(() => service.CreateCropPlanRequestAsync(
            NewRequest(data) with { FieldId = otherField.Id }, CancellationToken.None));
        Assert.Equal("FIELD_FARM_MISMATCH", fieldError.Code);
        var varietyError = await Assert.ThrowsAsync<ApiException>(() => service.CreateCropPlanRequestAsync(
            NewRequest(data) with { CropVarietyId = wrongVariety.Id }, CancellationToken.None));
        Assert.Equal("INVALID_CROP_VARIETY", varietyError.Code);
    }

    [Fact]
    public async Task Farmer_request_rejects_invalid_dates_budget_and_status_change()
    {
        await using var db = NewDbContext();
        var data = await SeedPlanAsync(db, CropPlanRequestStatus.Submitted);
        var service = NewService(db, data.Farmer.Id, new PlannedAiClient());
        var invalid = await Assert.ThrowsAsync<ApiException>(() => service.CreateCropPlanRequestAsync(
            NewRequest(data) with { PreferredEndDate = new DateOnly(2026, 10, 1), Budget = 0 }, CancellationToken.None));
        Assert.Equal("VALIDATION_ERROR", invalid.Code);
        var status = await Assert.ThrowsAsync<ApiException>(() => service.UpdateCropPlanRequestAsync(data.Request.Id,
            new CropPlanRequestUpdate(new DateOnly(2026, 10, 1), new DateOnly(2027, 1, 1), 12000,
                "Farmer objective", CropPlanRequestStatus.Approved), CancellationToken.None));
        Assert.Equal("CROP_PLAN_STATUS_MANAGED", status.Code);
    }

    [Fact]
    public async Task Preliminary_submission_waits_for_successful_coordinator_before_advancing_status()
    {
        await using var db = NewDbContext();
        var data = await SeedPlanAsync(db, CropPlanRequestStatus.Cancelled);
        var service = NewService(db, data.Farmer.Id, new MissingReferenceAiClient());
        var request = await service.GeneratePreliminaryRequestAsync(NewRequest(data), CancellationToken.None);

        Assert.Equal(CropPlanRequestStatus.Submitted, request.Status);
        var result = await service.StartAiWorkflowAsync(request.Id, CancellationToken.None);
        Assert.Equal("ReferenceDataUnavailable", result.Status);
        Assert.Equal(CropPlanRequestStatus.Submitted, (await db.CropPlanRequests.SingleAsync(item => item.Id == request.Id)).Status);
    }

    [Fact]
    public async Task Farmer_catalog_hides_inactive_crops_and_varieties_and_admin_can_manage_sources()
    {
        await using var db = NewDbContext();
        var data = await SeedPlanAsync(db, CropPlanRequestStatus.Submitted);
        var admin = NewService(db, data.Farmer.Id, new PlannedAiClient(), ApplicationRole.Admin);
        var farmer = NewService(db, data.Farmer.Id, new PlannedAiClient());
        var cropId = data.Request.CropTypeId;

        var active = await admin.CreateCropVarietyAsync(new CropVarietyRequest(cropId, "Bg 352", true), CancellationToken.None);
        await admin.CreateCropVarietyAsync(new CropVarietyRequest(cropId, "Old variety", false), CancellationToken.None);
        var visible = await farmer.SearchCropVarietiesAsync(new PagedQuery(), cropId, CancellationToken.None);
        Assert.Equal(active.Id, Assert.Single(visible.Items).Id);
        Assert.Equal(2, (await admin.SearchCropVarietiesAsync(new PagedQuery(), cropId, CancellationToken.None, true)).TotalCount);
        var forbidden = await Assert.ThrowsAsync<ApiException>(() => farmer.CreateCropVarietyAsync(
            new CropVarietyRequest(cropId, "Unauthorized", true), CancellationToken.None));
        Assert.Equal("ADMIN_REQUIRED", forbidden.Code);

        var profile = await admin.CreateReferenceProfileAsync(new CropReferenceProfileRequest(
            cropId, active.Id, "Sri Lanka", "Verified source", null, "1", DateTime.UtcNow.AddDays(-1),
            [new CropReferenceStageRequest("Establishment", 1, 1, 30, "Source verified")], []), CancellationToken.None);
        Assert.Equal("Bg 352", profile.VarietyName);
        Assert.Equal(1, profile.StageCount);
        Assert.Equal(1, (await admin.SearchReferenceProfilesAsync(new PagedQuery(), cropId, CancellationToken.None)).TotalCount);
    }

    [Fact]
    public async Task Start_workflow_persists_coordinator_output_and_marks_member2_step_ready()
    {
        await using var db = NewDbContext();
        var data = await SeedPlanAsync(db, CropPlanRequestStatus.Submitted);
        var service = NewService(db, data.Farmer.Id, new PlannedAiClient());

        var started = await service.StartAiWorkflowAsync(data.Request.Id, CancellationToken.None);

        Assert.Equal("Planned", started.Status);
        var workflow = await db.AgentWorkflows.Include(item => item.Steps).SingleAsync();
        Assert.Equal(AgentWorkflowStatus.Pending, workflow.Status);
        Assert.Equal("CropFieldAnalysisAgent", workflow.CurrentStep);
        Assert.Equal(CropPlanRequestStatus.PreliminaryGenerated, (await db.CropPlanRequests.SingleAsync()).Status);
        Assert.Contains(workflow.Steps, step => step.Sequence == 1 && step.AgentName == "CropPlanningCoordinatorAgent" && step.Status == AgentStepStatus.Completed);
        Assert.Contains(workflow.Steps, step => step.Sequence == 2 && step.AgentName == "CropFieldAnalysisAgent" && step.Status == AgentStepStatus.Pending);
        Assert.Contains(workflow.Steps, step => step.Sequence == 3 && step.AgentName == "WeatherResourceAgent" && step.Status == AgentStepStatus.Pending);
        Assert.Contains(workflow.Steps, step => step.Sequence == 4 && step.AgentName == "SchedulingValidationAgent" && step.Status == AgentStepStatus.Pending);
        Assert.Empty(await db.FarmTasks.ToListAsync());
        Assert.Empty(await db.IrrigationSchedules.ToListAsync());
        Assert.Empty(await db.ResourceReservations.ToListAsync());
    }

    [Fact]
    public async Task Run_field_analysis_persists_output_and_creates_member3_handoff()
    {
        await using var db = NewDbContext();
        var data = await SeedPlanAsync(db, CropPlanRequestStatus.Submitted);
        await NewService(db, data.Farmer.Id, new PlannedAiClient())
            .StartAiWorkflowAsync(data.Request.Id, CancellationToken.None);
        var assessmentService = NewService(db, data.Farmer.Id, new PlannedAiClient(), ApplicationRole.FieldOfficer);
        var saved = await assessmentService.SavePrePlantingAssessmentAsync(data.Request.Id, ValidAssessment(), CancellationToken.None);
        await assessmentService.SubmitPrePlantingAssessmentAsync(data.Request.Id, CancellationToken.None);
        var issue = new CropIssue
        {
            FieldInspectionId = saved.InspectionId,
            Title = "Field access constraint",
            Description = "Equipment access is restricted near the lower boundary.",
            Severity = CropIssueSeverity.High,
            Status = CropIssueStatus.Open
        };
        db.Add(issue);
        await db.SaveChangesAsync();
        var service = NewService(db, data.Farmer.Id, new FieldAnalysisAiClient(saved.InspectionId, issue.Id, data.Request.Id), ApplicationRole.FieldOfficer);

        var result = await service.RunFieldAnalysisAsync(data.Request.Id, CancellationToken.None);
        var handoff = await service.GetMember3HandoffAsync(data.Request.Id, CancellationToken.None);

        Assert.Equal("Analyzed", result.Status);
        var workflow = await db.AgentWorkflows.Include(item => item.Steps).SingleAsync();
        Assert.Equal(AgentWorkflowStatus.Pending, workflow.Status);
        Assert.Equal("WeatherResourceAgent", workflow.CurrentStep);
        Assert.Contains(workflow.Steps, step => step.AgentName == "CropFieldAnalysisAgent" && step.Status == AgentStepStatus.Completed);
        Assert.Equal("High", handoff.Priority);
        Assert.Contains(saved.InspectionId, handoff.EvidenceInspectionIds);
        Assert.Equal(issue.Id, handoff.OpenIssues.Single().IssueId);
    }

    [Fact]
    public async Task Safe_failure_remains_retryable_and_successful_retry_advances_exactly_once()
    {
        await using var db = NewDbContext();
        var data = await SeedPlanAsync(db, CropPlanRequestStatus.Submitted);
        await NewService(db, data.Farmer.Id, new PlannedAiClient())
            .StartAiWorkflowAsync(data.Request.Id, CancellationToken.None);
        var setup = NewService(db, data.Farmer.Id, new PlannedAiClient(), ApplicationRole.FieldOfficer);
        var assessment = await setup.SavePrePlantingAssessmentAsync(data.Request.Id, ValidAssessment(), CancellationToken.None);
        await setup.SubmitPrePlantingAssessmentAsync(data.Request.Id, CancellationToken.None);
        var client = new RetryingFieldAnalysisAiClient(assessment.InspectionId);
        var service = NewService(db, data.Farmer.Id, client, ApplicationRole.FieldOfficer);
        var originalVersion = (await db.AgentWorkflows.SingleAsync()).Version;

        var failed = await service.RunFieldAnalysisAsync(data.Request.Id, CancellationToken.None);

        Assert.Equal("SafeFailure", failed.Status);
        var workflow = await db.AgentWorkflows.Include(item => item.Steps).SingleAsync();
        var step = workflow.Steps.Single(item => item.AgentName == "CropFieldAnalysisAgent");
        Assert.Equal(AgentWorkflowStatus.Pending, workflow.Status);
        Assert.Equal("CropFieldAnalysisAgent", workflow.CurrentStep);
        Assert.Null(workflow.CompletedAt);
        Assert.Equal(AgentStepStatus.Failed, step.Status);
        Assert.Equal("AI_SAFE_FAILURE", step.ErrorCode);

        var succeeded = await service.RunFieldAnalysisAsync(data.Request.Id, CancellationToken.None);
        var repeated = await service.RunFieldAnalysisAsync(data.Request.Id, CancellationToken.None);

        Assert.Equal("Analyzed", succeeded.Status);
        Assert.Equal("Analyzed", repeated.Status);
        Assert.Equal(2, client.FieldAnalysisCalls);
        Assert.Equal(AgentStepStatus.Completed, step.Status);
        Assert.Equal(1, step.RetryCount);
        Assert.Equal("WeatherResourceAgent", workflow.CurrentStep);
        Assert.Equal(AgentWorkflowStatus.Pending, workflow.Status);
        Assert.Null(workflow.CompletedAt);
        Assert.True(workflow.Version >= originalVersion + 4);
    }

    [Fact]
    public async Task Field_analysis_revalidates_persisted_observations_and_owner_before_calling_ai()
    {
        await using var db = NewDbContext();
        var data = await SeedPlanAsync(db, CropPlanRequestStatus.Submitted);
        await NewService(db, data.Farmer.Id, new PlannedAiClient())
            .StartAiWorkflowAsync(data.Request.Id, CancellationToken.None);
        var setup = NewService(db, data.Farmer.Id, new PlannedAiClient(), ApplicationRole.FieldOfficer);
        var assessment = await setup.SavePrePlantingAssessmentAsync(data.Request.Id, ValidAssessment(), CancellationToken.None);
        await setup.SubmitPrePlantingAssessmentAsync(data.Request.Id, CancellationToken.None);
        var client = new CountingFieldAnalysisAiClient(assessment.InspectionId);
        var service = NewService(db, data.Farmer.Id, client, ApplicationRole.FieldOfficer);
        var moisture = await db.InspectionObservations.SingleAsync(item => item.FieldInspectionId == assessment.InspectionId && item.ObservationType == "SoilMoisture");
        moisture.Notes = "Malformed";
        await db.SaveChangesAsync();

        var invalid = await Assert.ThrowsAsync<ApiException>(() => service.RunFieldAnalysisAsync(data.Request.Id, CancellationToken.None));

        Assert.Equal("PREPLANT_ASSESSMENT_INVALID", invalid.Code);
        Assert.Equal(0, client.FieldAnalysisCalls);
        moisture.Notes = PrePlantingSoilMoisture.Moist.ToString();
        var inspection = await db.FieldInspections.SingleAsync(item => item.Id == assessment.InspectionId);
        inspection.InspectorUserId = Guid.NewGuid();
        await db.SaveChangesAsync();

        var wrongOwner = await Assert.ThrowsAsync<ApiException>(() => service.RunFieldAnalysisAsync(data.Request.Id, CancellationToken.None));

        Assert.Equal("PREPLANT_ASSESSMENT_OWNER_REQUIRED", wrongOwner.Code);
        Assert.Equal(0, client.FieldAnalysisCalls);
        var workflow = await db.AgentWorkflows.Include(item => item.Steps).SingleAsync();
        Assert.Equal("CropFieldAnalysisAgent", workflow.CurrentStep);
        Assert.Equal(AgentStepStatus.Pending, workflow.Steps.Single(item => item.AgentName == "CropFieldAnalysisAgent").Status);
    }

    [Fact]
    public async Task Concurrent_run_is_rejected_after_one_request_acquires_the_versioned_step()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        Guid requestId;
        Guid officerId;
        Guid inspectionId;
        await using (var setupDb = new AppDbContext(options))
        {
            var data = await SeedPlanAsync(setupDb, CropPlanRequestStatus.Submitted);
            requestId = data.Request.Id;
            officerId = data.Farmer.Id;
            await NewService(setupDb, officerId, new PlannedAiClient()).StartAiWorkflowAsync(requestId, CancellationToken.None);
            var setup = NewService(setupDb, officerId, new PlannedAiClient(), ApplicationRole.FieldOfficer);
            inspectionId = (await setup.SavePrePlantingAssessmentAsync(requestId, ValidAssessment(), CancellationToken.None)).InspectionId;
            await setup.SubmitPrePlantingAssessmentAsync(requestId, CancellationToken.None);
        }

        var client = new BlockingFieldAnalysisAiClient(inspectionId);
        await using var firstDb = new AppDbContext(options);
        await using var secondDb = new AppDbContext(options);
        var firstService = NewService(firstDb, officerId, client, ApplicationRole.FieldOfficer);
        var secondService = NewService(secondDb, officerId, client, ApplicationRole.FieldOfficer);
        var firstRun = firstService.RunFieldAnalysisAsync(requestId, CancellationToken.None);
        await client.Entered;

        var conflict = await Assert.ThrowsAsync<ApiException>(() => secondService.RunFieldAnalysisAsync(requestId, CancellationToken.None));
        Assert.Equal("FIELD_ANALYSIS_ALREADY_RUNNING", conflict.Code);
        Assert.Equal(1, client.FieldAnalysisCalls);

        client.Release();
        var completed = await firstRun;
        Assert.Equal("Analyzed", completed.Status);
        Assert.Equal(1, client.FieldAnalysisCalls);
    }

    [Fact]
    public async Task Field_officer_saves_incomplete_draft_and_preserves_null_versus_empty_risks()
    {
        await using var db = NewDbContext();
        var data = await SeedPlanAsync(db, CropPlanRequestStatus.Submitted);
        await NewService(db, data.Farmer.Id, new PlannedAiClient())
            .StartAiWorkflowAsync(data.Request.Id, CancellationToken.None);
        var service = NewService(db, data.Farmer.Id, new PlannedAiClient(), ApplicationRole.FieldOfficer);

        var unassessed = await service.SavePrePlantingAssessmentAsync(
            data.Request.Id,
            new PrePlantingAssessmentRequest { SoilType = PrePlantingSoilType.Loamy },
            CancellationToken.None);

        Assert.Equal(InspectionStatus.InProgress, unassessed.Status);
        Assert.Null(unassessed.IdentifiedRisks);
        Assert.Single(await db.InspectionObservations.ToListAsync());

        var assessedNone = await service.SavePrePlantingAssessmentAsync(
            data.Request.Id,
            new PrePlantingAssessmentRequest
            {
                SoilType = PrePlantingSoilType.Loamy,
                IdentifiedRisks = []
            },
            CancellationToken.None);

        Assert.NotNull(assessedNone.IdentifiedRisks);
        Assert.Empty(assessedNone.IdentifiedRisks);
        Assert.Single(await db.FieldInspections.ToListAsync());
        Assert.Equal(1, await db.InspectionObservations.CountAsync(item => item.ObservationType == "IdentifiedRisksAssessment"));
        Assert.Equal(0, await db.InspectionObservations.CountAsync(item => item.ObservationType == "IdentifiedRisk"));

        var assessedRisks = await service.SavePrePlantingAssessmentAsync(
            data.Request.Id,
            new PrePlantingAssessmentRequest
            {
                SoilType = PrePlantingSoilType.Loamy,
                IdentifiedRisks = [PrePlantingRisk.PoorDrainage, PrePlantingRisk.SoilErosion]
            },
            CancellationToken.None);

        Assert.Equal([PrePlantingRisk.PoorDrainage, PrePlantingRisk.SoilErosion], assessedRisks.IdentifiedRisks);
        Assert.Equal(1, await db.InspectionObservations.CountAsync(item => item.ObservationType == "IdentifiedRisksAssessment"));
        Assert.Equal(2, await db.InspectionObservations.CountAsync(item => item.ObservationType == "IdentifiedRisk"));
    }

    [Fact]
    public async Task Field_officer_saves_linked_pre_planting_assessment_and_officers_can_view_it()
    {
        await using var db = NewDbContext();
        var data = await SeedPlanAsync(db, CropPlanRequestStatus.Submitted);
        await NewService(db, data.Farmer.Id, new PlannedAiClient())
            .StartAiWorkflowAsync(data.Request.Id, CancellationToken.None);
        var fieldOfficerService = NewService(db, data.Farmer.Id, new PlannedAiClient(), ApplicationRole.FieldOfficer);

        var saved = await fieldOfficerService.SavePrePlantingAssessmentAsync(
            data.Request.Id,
            ValidAssessment(),
            CancellationToken.None);

        Assert.Equal(data.Request.Id, saved.CropPlanRequestId);
        Assert.Equal(data.Field.Id, saved.FieldId);
        Assert.Equal(InspectionStatus.InProgress, saved.Status);
        Assert.Equal(data.Farmer.Id, saved.InspectorUserId);
        var inspection = await db.FieldInspections.SingleAsync();
        Assert.Equal(InspectionPurpose.PrePlanting, inspection.Purpose);
        Assert.Equal(data.Request.Id, inspection.CropPlanRequestId);
        Assert.Equal(19, await db.InspectionObservations.CountAsync());

        var viewerService = NewService(db, data.Farmer.Id, new PlannedAiClient(), ApplicationRole.AgriculturalOfficer);
        var viewed = await viewerService.GetPrePlantingAssessmentAsync(data.Request.Id, CancellationToken.None);
        Assert.NotNull(viewed);
        Assert.Equal(PrePlantingSoilCondition.Good, viewed.SoilCondition);
        Assert.Equal(PrePlantingPlantingReadiness.ReadyWithMinorPreparation, viewed.PlantingReadiness);

        var resourceOfficerService = NewService(db, data.Farmer.Id, new PlannedAiClient(), ApplicationRole.ResourceOfficer);
        var resourceOfficerError = await Assert.ThrowsAsync<ApiException>(() =>
            resourceOfficerService.GetPrePlantingAssessmentAsync(data.Request.Id, CancellationToken.None));
        Assert.Equal("FIELD_ASSESSMENT_VIEWER_REQUIRED", resourceOfficerError.Code);
    }

    [Fact]
    public async Task Only_owning_field_officer_can_update_or_submit_a_draft()
    {
        await using var db = NewDbContext();
        var data = await SeedPlanAsync(db, CropPlanRequestStatus.Submitted);
        await NewService(db, data.Farmer.Id, new PlannedAiClient())
            .StartAiWorkflowAsync(data.Request.Id, CancellationToken.None);
        var owner = NewService(db, data.Farmer.Id, new PlannedAiClient(), ApplicationRole.FieldOfficer);
        await owner.SavePrePlantingAssessmentAsync(data.Request.Id, ValidAssessment(), CancellationToken.None);
        var otherOfficer = NewService(db, Guid.NewGuid(), new PlannedAiClient(), ApplicationRole.FieldOfficer);

        var saveError = await Assert.ThrowsAsync<ApiException>(() => otherOfficer.SavePrePlantingAssessmentAsync(
            data.Request.Id, ValidAssessment(), CancellationToken.None));
        var submitError = await Assert.ThrowsAsync<ApiException>(() => otherOfficer.SubmitPrePlantingAssessmentAsync(
            data.Request.Id, CancellationToken.None));

        Assert.Equal("PREPLANT_ASSESSMENT_OWNER_REQUIRED", saveError.Code);
        Assert.Equal("PREPLANT_ASSESSMENT_OWNER_REQUIRED", submitError.Code);
    }

    [Fact]
    public async Task Submission_reloads_validates_completes_and_is_idempotent_without_advancing_workflow()
    {
        await using var db = NewDbContext();
        var data = await SeedPlanAsync(db, CropPlanRequestStatus.Submitted);
        await NewService(db, data.Farmer.Id, new PlannedAiClient())
            .StartAiWorkflowAsync(data.Request.Id, CancellationToken.None);
        var service = NewService(db, data.Farmer.Id, new PlannedAiClient(), ApplicationRole.FieldOfficer);
        await service.SavePrePlantingAssessmentAsync(data.Request.Id, new PrePlantingAssessmentRequest(), CancellationToken.None);

        var invalid = await Assert.ThrowsAsync<ApiException>(() => service.SubmitPrePlantingAssessmentAsync(
            data.Request.Id, CancellationToken.None));
        Assert.Equal("PREPLANT_ASSESSMENT_INVALID", invalid.Code);

        await service.SavePrePlantingAssessmentAsync(data.Request.Id, ValidAssessment(), CancellationToken.None);
        var submitted = await service.SubmitPrePlantingAssessmentAsync(data.Request.Id, CancellationToken.None);

        Assert.Equal(InspectionStatus.Completed, submitted.Status);
        Assert.NotNull(submitted.CompletedAt);
        var workflow = await db.AgentWorkflows.Include(item => item.Steps).SingleAsync();
        Assert.Equal("CropFieldAnalysisAgent", workflow.CurrentStep);
        Assert.Equal(AgentStepStatus.Pending, workflow.Steps.Single(item => item.AgentName == "CropFieldAnalysisAgent").Status);

        workflow.CurrentStep = "WeatherResourceAgent";
        await db.SaveChangesAsync();
        var repeated = await service.SubmitPrePlantingAssessmentAsync(data.Request.Id, CancellationToken.None);

        Assert.Equal(submitted.InspectionId, repeated.InspectionId);
        Assert.Equal(submitted.CompletedAt, repeated.CompletedAt);
        Assert.Equal("WeatherResourceAgent", workflow.CurrentStep);

        var immutable = await Assert.ThrowsAsync<ApiException>(() => service.SavePrePlantingAssessmentAsync(
            data.Request.Id, ValidAssessment() with { OfficerNotes = "Changed after submission" }, CancellationToken.None));
        Assert.Equal("PREPLANT_ASSESSMENT_SUBMITTED", immutable.Code);
    }

    [Fact]
    public async Task Submission_rejects_changed_field_linkage()
    {
        await using var db = NewDbContext();
        var data = await SeedPlanAsync(db, CropPlanRequestStatus.Submitted);
        await NewService(db, data.Farmer.Id, new PlannedAiClient())
            .StartAiWorkflowAsync(data.Request.Id, CancellationToken.None);
        var service = NewService(db, data.Farmer.Id, new PlannedAiClient(), ApplicationRole.FieldOfficer);
        await service.SavePrePlantingAssessmentAsync(data.Request.Id, ValidAssessment(), CancellationToken.None);
        var inspection = await db.FieldInspections.SingleAsync();
        inspection.FieldId = Guid.NewGuid();
        await db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<ApiException>(() => service.SubmitPrePlantingAssessmentAsync(
            data.Request.Id, CancellationToken.None));

        Assert.Equal("PREPLANT_ASSESSMENT_LINKAGE_INVALID", error.Code);
    }

    [Fact]
    public async Task Pre_planting_context_returns_exact_plan_and_workflow_details()
    {
        await using var db = NewDbContext();
        var data = await SeedPlanAsync(db, CropPlanRequestStatus.Submitted);
        var variety = new CropVariety { CropTypeId = data.Request.CropTypeId, Name = "Bg 352", IsActive = true };
        data.Request.CropVariety = variety;
        data.Request.CultivationSeason = CultivationSeason.Maha;
        db.Add(variety);
        await db.SaveChangesAsync();
        await NewService(db, data.Farmer.Id, new PlannedAiClient())
            .StartAiWorkflowAsync(data.Request.Id, CancellationToken.None);
        var service = NewService(db, data.Farmer.Id, new PlannedAiClient(), ApplicationRole.FieldOfficer);

        var context = await service.GetPrePlantingContextAsync(data.Request.Id, CancellationToken.None);

        Assert.Equal(data.Request.Id, context.CropPlanRequestId);
        Assert.Equal(data.Farmer.Id, context.FarmerId);
        Assert.Equal("Farmer", context.FarmerName);
        Assert.Equal("North Farm", context.FarmName);
        Assert.Equal(data.Field.Id, context.FieldId);
        Assert.Equal("Field A", context.FieldName);
        Assert.Equal("Rice", context.CropName);
        Assert.Equal("Bg 352", context.CropVarietyName);
        Assert.Equal(CultivationSeason.Maha, context.CultivationSeason);
        Assert.Equal("CropFieldAnalysisAgent", context.CurrentStep);
    }

    [Fact]
    public async Task Only_field_officer_can_save_pre_planting_assessment()
    {
        await using var db = NewDbContext();
        var data = await SeedPlanAsync(db, CropPlanRequestStatus.Submitted);
        await NewService(db, data.Farmer.Id, new PlannedAiClient())
            .StartAiWorkflowAsync(data.Request.Id, CancellationToken.None);
        var agriculturalOfficerService = NewService(db, data.Farmer.Id, new PlannedAiClient(), ApplicationRole.AgriculturalOfficer);

        var exception = await Assert.ThrowsAsync<ApiException>(() => agriculturalOfficerService.SavePrePlantingAssessmentAsync(
            data.Request.Id,
            ValidAssessment(),
            CancellationToken.None));

        Assert.Equal("FIELD_OFFICER_REQUIRED", exception.Code);
        Assert.Empty(await db.FieldInspections.ToListAsync());
    }

    [Fact]
    public async Task Field_analysis_requires_exact_linked_assessment_to_be_submitted()
    {
        await using var db = NewDbContext();
        var data = await SeedPlanAsync(db, CropPlanRequestStatus.Submitted);
        await NewService(db, data.Farmer.Id, new PlannedAiClient())
            .StartAiWorkflowAsync(data.Request.Id, CancellationToken.None);
        var fieldOfficerService = NewService(db, data.Farmer.Id, new PlannedAiClient(), ApplicationRole.FieldOfficer);

        var missing = await Assert.ThrowsAsync<ApiException>(() => fieldOfficerService.RunFieldAnalysisAsync(data.Request.Id, CancellationToken.None));
        Assert.Equal("PREPLANT_ASSESSMENT_REQUIRED", missing.Code);

        await fieldOfficerService.SavePrePlantingAssessmentAsync(data.Request.Id, ValidAssessment(), CancellationToken.None);
        var draft = await Assert.ThrowsAsync<ApiException>(() => fieldOfficerService.RunFieldAnalysisAsync(data.Request.Id, CancellationToken.None));
        Assert.Equal("PREPLANT_ASSESSMENT_NOT_SUBMITTED", draft.Code);
    }

    [Fact]
    public async Task Start_workflow_persists_missing_reference_state_without_downstream_steps()
    {
        await using var db = NewDbContext();
        var data = await SeedPlanAsync(db, CropPlanRequestStatus.PreliminaryGenerated);
        var service = NewService(db, data.Farmer.Id, new MissingReferenceAiClient());

        var started = await service.StartAiWorkflowAsync(data.Request.Id, CancellationToken.None);
        var result = await service.GetPlanningResultAsync(data.Request.Id, CancellationToken.None);

        Assert.Equal("ReferenceDataUnavailable", started.Status);
        Assert.True(started.RequiresHumanReview);
        Assert.Equal("Unavailable", result.ReferenceDataStatus);
        Assert.Empty(result.Steps);
        Assert.Single(await db.AgentSteps.ToListAsync());
    }

    [Fact]
    public async Task Start_workflow_records_safe_failure_when_ai_service_is_unavailable()
    {
        await using var db = NewDbContext();
        var data = await SeedPlanAsync(db, CropPlanRequestStatus.Submitted);
        var service = NewService(db, data.Farmer.Id, new ThrowingAiClient());

        var started = await service.StartAiWorkflowAsync(data.Request.Id, CancellationToken.None);

        Assert.Equal("SafeFailure", started.Status);
        Assert.True(started.RequiresHumanReview);
        var workflow = await db.AgentWorkflows.Include(item => item.Steps).SingleAsync();
        Assert.Equal(AgentWorkflowStatus.Failed, workflow.Status);
        Assert.Equal(AgentStepStatus.Failed, workflow.Steps.Single().Status);
        Assert.Equal("AI_SERVICE_UNAVAILABLE", workflow.Steps.Single().ErrorCode);
        Assert.False((await db.AgentValidationResults.SingleAsync()).IsValid);
    }

    private static AppDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CropPlanningService NewService(
        AppDbContext db,
        Guid userId,
        IAgenticAIClient client,
        ApplicationRole role = ApplicationRole.Farmer) =>
        new(
            db,
            new FixedCurrentUserService(role, userId),
            new FarmRequestValidator(),
            new FieldRequestValidator(),
            new CropTypeRequestValidator(),
            new CropCycleRequestValidator(),
            new CropPlanRequestCreateValidator(),
            new CropPlanRequestUpdateValidator(),
            client);

    private static async Task<SeededPlan> SeedPlanAsync(AppDbContext db, CropPlanRequestStatus status)
    {
        var farmer = new AppUser { FullName = "Farmer", Email = "farmer.ai@example.test", PasswordHash = "hash", Role = ApplicationRole.Farmer, IsActive = true };
        var farm = new Farm { Name = "North Farm", Location = "North", TotalArea = 10, OwnerUser = farmer };
        var field = new Field { Name = "Field A", Area = 2, SoilType = "Loam", Farm = farm, IsActive = true };
        var cropType = new CropType { Name = "Rice", IsActive = true };
        var request = new CropPlanRequest
        {
            Farm = farm,
            Field = field,
            CropType = cropType,
            RequestedByUser = farmer,
            PreferredStartDate = new DateOnly(2026, 10, 1),
            PreferredEndDate = new DateOnly(2027, 1, 1),
            Budget = 12000,
            Objective = "Plan the next rice season safely.",
            Status = status
        };
        db.AddRange(farmer, farm, field, cropType, request);
        await db.SaveChangesAsync();
        return new SeededPlan(farmer, request, field);
    }

    private static CropPlanRequestCreate NewRequest(SeededPlan data) =>
        new(data.Request.FarmId, data.Field.Id, data.Request.CropTypeId,
            new DateOnly(2026, 10, 1), new DateOnly(2027, 1, 1), 12000, "Farmer objective");

    private static PrePlantingAssessmentRequest ValidAssessment() =>
        new()
        {
            SoilType = PrePlantingSoilType.Loamy,
            SoilCondition = PrePlantingSoilCondition.Good,
            SoilMoisture = PrePlantingSoilMoisture.Moist,
            SoilNotes = "Moist loam with no visible compaction.",
            WaterAvailability = PrePlantingWaterAvailability.Adequate,
            MainWaterSource = "Canal",
            IrrigationAvailability = PrePlantingIrrigationAvailability.Available,
            WaterReliability = PrePlantingWaterReliability.Reliable,
            WaterConcerns = "Supply should be rechecked before sowing.",
            DrainageCondition = PrePlantingDrainageCondition.Good,
            WaterloggingRisk = PrePlantingWaterloggingRisk.Low,
            DrainageNotes = "Drainage channels are clear.",
            GeneralFieldCondition = PrePlantingGeneralFieldCondition.ClearAndPrepared,
            GeneralFieldNotes = "Field is cleared and level.",
            PlantingReadiness = PrePlantingPlantingReadiness.ReadyWithMinorPreparation,
            IdentifiedRisks = [PrePlantingRisk.LandPreparationRequired],
            RiskNotes = "Final harrowing remains.",
            OfficerNotes = "Recheck the low area before sowing."
        };

    private sealed record SeededPlan(AppUser Farmer, CropPlanRequest Request, Field Field);

    private sealed class FixedCurrentUserService(ApplicationRole role, Guid userId) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;
        public ApplicationRole? Role { get; } = role;
        public bool IsInRole(ApplicationRole roleToCheck) => Role == roleToCheck;
    }

    private class PlannedAiClient : IAgenticAIClient
    {
        public virtual Task<CropPlanningCoordinatorOutput> RunCropPlanningCoordinatorAsync(CropPlanningCoordinatorInput input, CancellationToken cancellationToken) =>
            Task.FromResult(new CropPlanningCoordinatorOutput(
                input.WorkflowId,
                "Planned",
                false,
                [],
                "Available",
                "Safe objective summary.",
                [
                    new CropPlanningDelegatedStepResponse(1, "FieldAnalysis", "CropFieldAnalysisAgent"),
                    new CropPlanningDelegatedStepResponse(2, "WeatherResourceAnalysis", "WeatherResourceAgent"),
                    new CropPlanningDelegatedStepResponse(3, "Scheduling", "SchedulingValidationAgent")
                ]));

        public virtual Task<FieldAnalysisOutput> RunFieldAnalysisAsync(FieldAnalysisInput input, CancellationToken cancellationToken) =>
            Task.FromResult(new FieldAnalysisOutput(input.WorkflowId, "SafeFailure", true, ["Not configured for this test."], new FieldAnalysisFieldConditionResponse(string.Empty, []), [], "Unknown"));
    }

    private sealed class RecordingAiClient : PlannedAiClient
    {
        public CropPlanningCoordinatorInput? LastInput { get; private set; }

        public override Task<CropPlanningCoordinatorOutput> RunCropPlanningCoordinatorAsync(CropPlanningCoordinatorInput input, CancellationToken cancellationToken)
        {
            LastInput = input;
            return base.RunCropPlanningCoordinatorAsync(input, cancellationToken);
        }
    }

    private sealed class FieldAnalysisAiClient(Guid inspectionId, Guid issueId, Guid cropPlanRequestId) : PlannedAiClient
    {
        public override Task<FieldAnalysisOutput> RunFieldAnalysisAsync(FieldAnalysisInput input, CancellationToken cancellationToken)
        {
            Assert.Equal(cropPlanRequestId, input.CropPlanRequestId);
            Assert.Equal(inspectionId, input.PrePlantingInspectionId);
            return Task.FromResult(new FieldAnalysisOutput(
                input.WorkflowId,
                "Analyzed",
                true,
                [],
                new FieldAnalysisFieldConditionResponse("Stored pre-planting evidence indicates an access constraint that needs review.", [inspectionId]),
                [new FieldAnalysisOpenIssueResponse(issueId, "High", "Open", inspectionId)],
                "High"));
        }
    }

    private class CountingFieldAnalysisAiClient(Guid inspectionId) : PlannedAiClient
    {
        protected Guid InspectionId { get; } = inspectionId;
        public int FieldAnalysisCalls { get; protected set; }

        public override Task<FieldAnalysisOutput> RunFieldAnalysisAsync(FieldAnalysisInput input, CancellationToken cancellationToken)
        {
            FieldAnalysisCalls++;
            return Task.FromResult(Success(input.WorkflowId, InspectionId));
        }

        protected static FieldAnalysisOutput Success(Guid workflowId, Guid evidenceInspectionId) =>
            new(
                workflowId,
                "Analyzed",
                false,
                [],
                new FieldAnalysisFieldConditionResponse("The submitted pre-planting assessment is ready for planning.", [evidenceInspectionId]),
                [],
                "Low");
    }

    private sealed class RetryingFieldAnalysisAiClient(Guid inspectionId) : CountingFieldAnalysisAiClient(inspectionId)
    {
        public override Task<FieldAnalysisOutput> RunFieldAnalysisAsync(FieldAnalysisInput input, CancellationToken cancellationToken)
        {
            FieldAnalysisCalls++;
            return Task.FromResult(FieldAnalysisCalls == 1
                ? new FieldAnalysisOutput(
                    input.WorkflowId,
                    "SafeFailure",
                    true,
                    ["The analysis provider could not produce a validated result."],
                    new FieldAnalysisFieldConditionResponse(string.Empty, []),
                    [],
                    "Unknown")
                : Success(input.WorkflowId, InspectionId));
        }
    }

    private sealed class BlockingFieldAnalysisAiClient(Guid inspectionId) : CountingFieldAnalysisAiClient(inspectionId)
    {
        private readonly TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => entered.Task;
        public void Release() => release.TrySetResult(true);

        public override async Task<FieldAnalysisOutput> RunFieldAnalysisAsync(FieldAnalysisInput input, CancellationToken cancellationToken)
        {
            FieldAnalysisCalls++;
            entered.TrySetResult(true);
            await release.Task.WaitAsync(cancellationToken);
            return Success(input.WorkflowId, InspectionId);
        }
    }

    private sealed class MissingReferenceAiClient : IAgenticAIClient
    {
        public Task<CropPlanningCoordinatorOutput> RunCropPlanningCoordinatorAsync(CropPlanningCoordinatorInput input, CancellationToken cancellationToken) =>
            Task.FromResult(new CropPlanningCoordinatorOutput(
                input.WorkflowId,
                "ReferenceDataUnavailable",
                true,
                ["Verified crop reference data is missing."],
                "Unavailable",
                string.Empty,
                []));

        public Task<FieldAnalysisOutput> RunFieldAnalysisAsync(FieldAnalysisInput input, CancellationToken cancellationToken) =>
            Task.FromResult(new FieldAnalysisOutput(input.WorkflowId, "SafeFailure", true, ["Reference data unavailable."], new FieldAnalysisFieldConditionResponse(string.Empty, []), [], "Unknown"));
    }

    private sealed class ThrowingAiClient : IAgenticAIClient
    {
        public Task<CropPlanningCoordinatorOutput> RunCropPlanningCoordinatorAsync(CropPlanningCoordinatorInput input, CancellationToken cancellationToken) =>
            throw new HttpRequestException("No service.");

        public Task<FieldAnalysisOutput> RunFieldAnalysisAsync(FieldAnalysisInput input, CancellationToken cancellationToken) =>
            throw new HttpRequestException("No service.");
    }
}
