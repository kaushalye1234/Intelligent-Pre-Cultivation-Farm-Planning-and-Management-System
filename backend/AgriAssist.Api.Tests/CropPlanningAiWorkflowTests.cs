using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
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
    }

    [Fact]
    public async Task Run_field_analysis_persists_output_and_creates_member3_handoff()
    {
        await using var db = NewDbContext();
        var data = await SeedPlanAsync(db, CropPlanRequestStatus.Submitted);
        var inspection = new FieldInspection
        {
            FieldId = data.Field.Id,
            CropPlanRequestId = data.Request.Id,
            Purpose = InspectionPurpose.PrePlanting,
            InspectorUserId = data.Farmer.Id,
            ScheduledAt = DateTime.UtcNow.AddDays(-1),
            CompletedAt = DateTime.UtcNow,
            Status = InspectionStatus.Completed,
            Summary = "Drainage risk observed near the low area."
        };
        var issue = new CropIssue { FieldInspection = inspection, Title = "Leaf yellowing", Description = "Yellowing observed.", Severity = CropIssueSeverity.High, Status = CropIssueStatus.Open };
        db.AddRange(inspection, issue);
        await db.SaveChangesAsync();
        var service = NewService(db, data.Farmer.Id, new FieldAnalysisAiClient(inspection.Id, issue.Id, data.Request.Id), ApplicationRole.FieldOfficer);
        await service.StartAiWorkflowAsync(data.Request.Id, CancellationToken.None);

        var result = await service.RunFieldAnalysisAsync(data.Request.Id, CancellationToken.None);
        var handoff = await service.GetMember3HandoffAsync(data.Request.Id, CancellationToken.None);

        Assert.Equal("Analyzed", result.Status);
        var workflow = await db.AgentWorkflows.Include(item => item.Steps).SingleAsync();
        Assert.Equal(AgentWorkflowStatus.Pending, workflow.Status);
        Assert.Equal("WeatherResourceAgent", workflow.CurrentStep);
        Assert.Contains(workflow.Steps, step => step.AgentName == "CropFieldAnalysisAgent" && step.Status == AgentStepStatus.Completed);
        Assert.Equal("High", handoff.Priority);
        Assert.Contains(inspection.Id, handoff.EvidenceInspectionIds);
        Assert.Equal(issue.Id, handoff.OpenIssues.Single().IssueId);
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
        var inspection = await db.FieldInspections.SingleAsync();
        Assert.Equal(InspectionPurpose.PrePlanting, inspection.Purpose);
        Assert.Equal(data.Request.Id, inspection.CropPlanRequestId);
        Assert.Equal(8, await db.InspectionObservations.CountAsync());

        var viewerService = NewService(db, data.Farmer.Id, new PlannedAiClient(), ApplicationRole.AgriculturalOfficer);
        var viewed = await viewerService.GetPrePlantingAssessmentAsync(data.Request.Id, CancellationToken.None);
        Assert.NotNull(viewed);
        Assert.Equal("Moist loam", viewed.SoilCondition);
        Assert.Equal("Ready after final harrowing", viewed.PlantingReadiness);

        var resourceOfficerService = NewService(db, data.Farmer.Id, new PlannedAiClient(), ApplicationRole.ResourceOfficer);
        var resourceOfficerError = await Assert.ThrowsAsync<ApiException>(() =>
            resourceOfficerService.GetPrePlantingAssessmentAsync(data.Request.Id, CancellationToken.None));
        Assert.Equal("FIELD_ASSESSMENT_VIEWER_REQUIRED", resourceOfficerError.Code);
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
        new(
            "Moist loam",
            "Canal supply available",
            "Pump is operational",
            "Drainage channels are clear",
            "Field is cleared and level",
            "Ready after final harrowing",
            "Low area may retain water",
            "Recheck the low area before sowing");

    private sealed record SeededPlan(AppUser Farmer, CropPlanRequest Request, Field Field);

    private sealed class FixedCurrentUserService(ApplicationRole role, Guid userId) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;
        public ApplicationRole? Role { get; } = role;
        public bool IsInRole(ApplicationRole roleToCheck) => Role == roleToCheck;
    }

    private class PlannedAiClient : IAgenticAIClient
    {
        public Task<CropPlanningCoordinatorOutput> RunCropPlanningCoordinatorAsync(CropPlanningCoordinatorInput input, CancellationToken cancellationToken) =>
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
                new FieldAnalysisFieldConditionResponse("Stored inspection evidence indicates yellowing that needs review.", [inspectionId]),
                [new FieldAnalysisOpenIssueResponse(issueId, "High", "Open", inspectionId)],
                "High"));
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
