using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.FinalCultivationGuide;
using AgriAssist.Api.ExternalServices.AgenticAI;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Models.TaskApproval;
using AgriAssist.Api.Services.FinalCultivationGuide;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgriAssist.Api.Tests;

public sealed class FinalCultivationGuideServiceTests
{
    [Fact]
    public async Task Generate_persists_one_revision_scoped_guide_and_returns_it_on_retry()
    {
        await using var db = NewDbContext();
        var data = await SeedApprovedWorkflowAsync(db);
        var client = new FakeGuideClient(input => Output(input.WorkflowId, input.ApprovedRevision));
        var service = NewService(db, client);

        var first = await service.GenerateAsync(data.Workflow.Id, 2, CancellationToken.None);
        var retry = await service.GenerateAsync(data.Workflow.Id, 2, CancellationToken.None);

        Assert.Equal("Ready", first.Status);
        Assert.Equal("Ready", retry.Status);
        Assert.Equal(1, client.CallCount);
        var guide = Assert.IsType<FinalCultivationGuideOutputDto>(retry.Guide);
        var approvedTask = Assert.Single(guide.ApprovedActivities);
        Assert.Equal(data.Task.Id, approvedTask.Id);
        Assert.Equal(data.Task.DueAt, approvedTask.ScheduledAt);
        Assert.Single(await db.AgentSteps.Where(item => item.AgentName == "FinalCultivationGuideAgent").ToListAsync());
    }

    [Fact]
    public async Task Generate_rejects_stale_revision_without_calling_ai()
    {
        await using var db = NewDbContext();
        var data = await SeedApprovedWorkflowAsync(db);
        var client = new FakeGuideClient(input => Output(input.WorkflowId, input.ApprovedRevision));

        var result = await NewService(db, client).GenerateAsync(data.Workflow.Id, 1, CancellationToken.None);

        Assert.Equal("Unavailable", result.Status);
        Assert.Equal(0, client.CallCount);
        Assert.DoesNotContain(db.AgentSteps, item => item.AgentName == "FinalCultivationGuideAgent");
    }

    [Fact]
    public async Task Generate_marks_guide_failed_without_changing_the_approved_workflow()
    {
        await using var db = NewDbContext();
        var data = await SeedApprovedWorkflowAsync(db);
        var client = new FakeGuideClient(_ => throw new HttpRequestException("provider unavailable"));

        var result = await NewService(db, client).GenerateAsync(data.Workflow.Id, 2, CancellationToken.None);

        Assert.Equal("Unavailable", result.Status);
        var workflow = await db.AgentWorkflows.SingleAsync(item => item.Id == data.Workflow.Id);
        Assert.Equal(AgentWorkflowStatus.Completed, workflow.Status);
        Assert.Equal(CropPlanRequestStatus.Approved,
            (await db.CropPlanRequests.SingleAsync(item => item.Id == data.Plan.Id)).Status);
        var guideStep = await db.AgentSteps.SingleAsync(item => item.AgentName == "FinalCultivationGuideAgent");
        Assert.Equal(AgentStepStatus.Failed, guideStep.Status);
        Assert.Equal("FINAL_GUIDE_UNAVAILABLE", guideStep.ErrorCode);
    }

    private static FinalCultivationGuideService NewService(AppDbContext db, IFinalCultivationGuideAIClient client) =>
        new(db, client, NullLogger<FinalCultivationGuideService>.Instance);

    private static AppDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<SeededGuideData> SeedApprovedWorkflowAsync(AppDbContext db)
    {
        var actorId = Guid.NewGuid();
        var farm = new Farm { Name = "Guide Farm", Location = "Kurunegala", OwnerUserId = actorId };
        var field = new Field { Farm = farm, Name = "Field A", SoilType = "Loam", Area = 2, IsActive = true };
        var crop = new CropType { Name = "Maize", IsActive = true };
        var plan = new CropPlanRequest
        {
            Farm = farm,
            Field = field,
            CropType = crop,
            RequestedByUserId = actorId,
            PreferredStartDate = new DateOnly(2026, 10, 1),
            PreferredEndDate = new DateOnly(2027, 2, 28),
            Objective = "Grow maize safely.",
            Status = CropPlanRequestStatus.Approved,
        };
        var workflow = new AgentWorkflow
        {
            CropPlanRequest = plan,
            InitiatedByUserId = actorId,
            Objective = plan.Objective,
            CandidateRevision = 2,
            Status = AgentWorkflowStatus.Completed,
            CurrentStep = "Completed",
            CompletedAt = DateTime.UtcNow,
            Steps =
            [
                CompletedStep("CropPlanningCoordinatorAgent", "Coordinator", 1, 1),
                CompletedStep("CropFieldAnalysisAgent", "FieldAnalysis", 2, 1),
                CompletedStep("WeatherResourceAgent", "WeatherResourceAnalysis", 3, 1),
                CompletedStep("SchedulingValidationAgent", "Scheduling", 4, 2),
            ],
        };
        var decision = new ApprovalDecision
        {
            AgentWorkflow = workflow,
            CandidateRevision = 2,
            DecidedByUserId = actorId,
            Decision = ApprovalDecisionType.Approved,
            Comment = "Approved.",
        };
        var task = new FarmTask
        {
            Farm = farm,
            Title = "Prepare the field",
            Description = "Approved work.",
            DueAt = new DateTime(2026, 10, 10, 8, 0, 0, DateTimeKind.Utc),
            AssignedToUserId = actorId,
            Status = FarmTaskStatus.Approved,
            GeneratedByWorkflow = workflow,
            CandidateRevision = 2,
        };
        db.AddRange(farm, field, crop, plan, workflow, decision, task);
        await db.SaveChangesAsync();
        return new SeededGuideData(plan, workflow, task);
    }

    private static AgentStep CompletedStep(string agentName, string stepName, int sequence, int revision) => new()
    {
        AgentName = agentName,
        StepName = stepName,
        Sequence = sequence,
        CandidateRevision = revision,
        Status = AgentStepStatus.Completed,
        OutputJson = "{}",
        CompletedAt = DateTime.UtcNow,
    };

    private static FinalCultivationGuideOutputDto Output(Guid workflowId, int revision) => new(
        1,
        workflowId,
        revision,
        ["Check field drainage."],
        "The crop is in an early growth stage.",
        [new FinalGuideMonthDto("2026-11", "Support healthy crop growth.", [], [])],
        [],
        ["Keep the harvest area clear."],
        "This guide uses approved crop and field evidence.");

    private sealed record SeededGuideData(CropPlanRequest Plan, AgentWorkflow Workflow, FarmTask Task);

    private sealed class FakeGuideClient(
        Func<FinalCultivationGuideInputDto, FinalCultivationGuideOutputDto> response) : IFinalCultivationGuideAIClient
    {
        public int CallCount { get; private set; }

        public Task<FinalCultivationGuideOutputDto> GenerateFinalCultivationGuideAsync(
            FinalCultivationGuideInputDto input,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(response(input));
        }
    }
}
