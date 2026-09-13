using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.ExternalServices.AgenticAI;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.CropPlanning;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.CropPlanning;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Tests;

public sealed class CropPlanningAiWorkflowTests
{
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

    private static CropPlanningService NewService(AppDbContext db, Guid userId, IAgenticAIClient client) =>
        new(
            db,
            new FixedCurrentUserService(ApplicationRole.Farmer, userId),
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
        return new SeededPlan(farmer, request);
    }

    private sealed record SeededPlan(AppUser Farmer, CropPlanRequest Request);

    private sealed class FixedCurrentUserService(ApplicationRole role, Guid userId) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;
        public ApplicationRole? Role { get; } = role;
        public bool IsInRole(ApplicationRole roleToCheck) => Role == roleToCheck;
    }

    private sealed class PlannedAiClient : IAgenticAIClient
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
    }

    private sealed class ThrowingAiClient : IAgenticAIClient
    {
        public Task<CropPlanningCoordinatorOutput> RunCropPlanningCoordinatorAsync(CropPlanningCoordinatorInput input, CancellationToken cancellationToken) =>
            throw new HttpRequestException("No service.");
    }
}
