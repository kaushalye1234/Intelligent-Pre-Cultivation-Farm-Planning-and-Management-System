using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.ExternalServices.AgenticAI;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Models.TaskApproval;
using AgriAssist.Api.Services.CropPlanning;
using AgriAssist.Api.Services.Resources;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.CropPlanning;
using Microsoft.EntityFrameworkCore;
using static AgriAssist.Api.Tests.WeatherResourceTestData;

namespace AgriAssist.Api.Tests;

/// <summary>
/// The Resource Officer work queue is a read-only projection: a crop plan is listed only while its latest
/// workflow's current step is WeatherResourceAgent. Nothing here is a persisted assignment.
/// </summary>
public sealed class WeatherResourceWorkQueueTests
{
    private static readonly DateTime BaseTime = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Queue_lists_only_the_latest_workflow_waiting_for_weather_resource_analysis()
    {
        await using var db = NewDbContext();
        var ready = await AddPlanAsync(db, "Green Farm", "Kurunegala", "Block A", "Tomato", "Rio Grande", "Plan tomatoes.", "WeatherResourceAgent", readyAt: BaseTime.AddHours(3));
        await AddPlanAsync(db, "Field Stage Farm", "Matale", "Block B", "Rice", null, "Waiting for Member 2.", "CropFieldAnalysisAgent", readyAt: null);
        await AddPlanAsync(db, "Scheduling Farm", "Galle", "Block C", "Chilli", null, "Already analysed.", "SchedulingValidationAgent", readyAt: BaseTime.AddHours(1));
        var movedOn = await AddPlanAsync(db, "Moved On Farm", "Badulla", "Block D", "Beans", null, "Superseded plan.", "WeatherResourceAgent", readyAt: BaseTime.AddHours(1), createdAt: BaseTime);
        await AddWorkflowAsync(db, movedOn.RequestId, "SchedulingValidationAgent", readyAt: BaseTime.AddHours(2), createdAt: BaseTime.AddDays(1));
        var rerun = await AddPlanAsync(db, "Rerun Farm", "Jaffna", "Block E", "Onion", null, "Earlier workflow stalled.", "CropFieldAnalysisAgent", readyAt: null, createdAt: BaseTime);
        var rerunLatest = await AddWorkflowAsync(db, rerun.RequestId, "WeatherResourceAgent", readyAt: BaseTime.AddHours(5), createdAt: BaseTime.AddDays(1));
        var deletedWorkflow = await AddPlanAsync(db, "Deleted Workflow Farm", "Ampara", "Block F", "Maize", null, "Deleted.", "WeatherResourceAgent", readyAt: BaseTime);
        (await db.AgentWorkflows.SingleAsync(item => item.Id == deletedWorkflow.WorkflowId)).IsDeleted = true;
        var deletedRequest = await AddPlanAsync(db, "Deleted Request Farm", "Ampara", "Block G", "Maize", null, "Deleted.", "WeatherResourceAgent", readyAt: BaseTime);
        (await db.CropPlanRequests.SingleAsync(item => item.Id == deletedRequest.RequestId)).IsDeleted = true;
        await db.SaveChangesAsync();

        var result = await NewService(db).GetWorkQueueAsync(new PagedQuery(), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal([ready.WorkflowId, rerunLatest.WorkflowId], result.Items.Select(item => item.WorkflowId));
        var item = result.Items[0];
        Assert.Equal(ready.RequestId, item.CropPlanRequestId);
        Assert.Equal(ready.WeatherStepId, item.WeatherResourceStepId);
        Assert.Equal("Plan tomatoes.", item.Objective);
        Assert.Equal(("Green Farm", "Kurunegala"), (item.FarmName, item.FarmLocation));
        Assert.Equal((ready.FieldId, "Block A"), (item.FieldId, item.FieldName));
        Assert.Equal(("Tomato", "Rio Grande"), (item.CropName, item.CropVarietyName));
        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2027, 1, 1)), (item.PreferredStartDate, item.PreferredEndDate));
        Assert.Equal((2, 4), (item.CandidateRevision, item.WorkflowVersion));
        Assert.Equal(AgentStepStatus.Pending, item.StepStatus);
        Assert.Equal(BaseTime.AddHours(3), item.ReadyAt);
        Assert.Null(item.StartedAt);
        Assert.Null(item.ErrorCode);
        Assert.Null(item.ErrorMessageSafe);
        Assert.Equal(rerun.RequestId, result.Items[1].CropPlanRequestId);
        Assert.Equal(rerunLatest.WeatherStepId, result.Items[1].WeatherResourceStepId);
    }

    [Fact]
    public async Task Queue_is_not_derived_from_pending_tasks_or_schedules()
    {
        await using var db = NewDbContext();
        var ready = await AddPlanAsync(db, "Green Farm", "Kurunegala", "Block A", "Tomato", null, "Plan tomatoes.", "WeatherResourceAgent", readyAt: BaseTime);
        var other = await AddPlanAsync(db, "Task Farm", "Matale", "Block B", "Rice", null, "Already scheduled.", "SchedulingValidationAgent", readyAt: BaseTime);
        var request = await db.CropPlanRequests.Include(item => item.Field).SingleAsync(item => item.Id == other.RequestId);
        var assignee = new AppUser { FullName = "Officer", Email = $"officer.{Guid.NewGuid():N}@example.test", PasswordHash = "hash", Role = ApplicationRole.AgriculturalOfficer, IsActive = true };
        db.AddRange(
            assignee,
            new FarmTask { FarmId = request.FarmId, Title = "Pending task", Description = "Awaiting approval.", DueAt = BaseTime.AddDays(3), AssignedToUser = assignee, Status = FarmTaskStatus.PendingApproval, GeneratedByWorkflowId = other.WorkflowId },
            new FarmTask { FarmId = request.FarmId, Title = "Pending task 2", Description = "Awaiting approval.", DueAt = BaseTime.AddDays(4), AssignedToUser = assignee, Status = FarmTaskStatus.PendingApproval },
            new IrrigationSchedule { FieldId = request.FieldId!.Value, ScheduledAt = BaseTime.AddDays(5), DurationMinutes = 30, Notes = "Pending schedule.", Status = IrrigationScheduleStatus.PendingApproval, GeneratedByWorkflowId = other.WorkflowId });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetWorkQueueAsync(new PagedQuery(), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(ready.WorkflowId, Assert.Single(result.Items).WorkflowId);
    }

    [Theory]
    [InlineData("green", "Green Farm")]
    [InlineData("KURUNEGALA", "Green Farm")]
    [InlineData("paddy block", "Paddy Farm")]
    [InlineData("chilli", "Chilli Farm")]
    [InlineData("rio grande", "Green Farm")]
    [InlineData("drought-tolerant", "Paddy Farm")]
    public async Task Search_matches_objective_farm_field_crop_and_variety(string search, string expectedFarm)
    {
        await using var db = NewDbContext();
        await SeedSearchDataAsync(db);

        var result = await NewService(db).GetWorkQueueAsync(new PagedQuery { Search = search }, CancellationToken.None);

        Assert.Equal(expectedFarm, Assert.Single(result.Items).FarmName);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_does_not_match_unsearchable_values()
    {
        await using var db = NewDbContext();
        await SeedSearchDataAsync(db);
        var step = await db.AgentSteps.FirstAsync(item => item.AgentName == "WeatherResourceAgent");
        step.ErrorMessageSafe = "hidden-error-text";
        await db.SaveChangesAsync();

        foreach (var search in new[] { "WeatherResourceAgent", "hidden-error-text", "Farmer" })
        {
            var result = await NewService(db).GetWorkQueueAsync(new PagedQuery { Search = search }, CancellationToken.None);
            Assert.Empty(result.Items);
            Assert.Equal(0, result.TotalCount);
        }
    }

    [Fact]
    public async Task Paging_is_normalised_and_stable()
    {
        await using var db = NewDbContext();
        var expected = new List<Guid>();
        for (var index = 0; index < 5; index++)
        {
            // Identical ReadyAt values force the WorkflowId tie-breaker.
            expected.Add((await AddPlanAsync(db, $"Farm {index}", "Kandy", $"Block {index}", "Tomato", null, "Plan.", "WeatherResourceAgent", readyAt: BaseTime)).WorkflowId);
        }
        var service = NewService(db);

        var first = await service.GetWorkQueueAsync(new PagedQuery { Page = 1, PageSize = 2 }, CancellationToken.None);
        var second = await service.GetWorkQueueAsync(new PagedQuery { Page = 2, PageSize = 2 }, CancellationToken.None);
        var third = await service.GetWorkQueueAsync(new PagedQuery { Page = 3, PageSize = 2 }, CancellationToken.None);
        var repeat = await service.GetWorkQueueAsync(new PagedQuery { Page = 1, PageSize = 2 }, CancellationToken.None);

        Assert.Equal((5, 3, 2), (first.TotalCount, first.TotalPages, first.Items.Count));
        Assert.Equal(first.Items.Select(item => item.WorkflowId), repeat.Items.Select(item => item.WorkflowId));
        var all = first.Items.Concat(second.Items).Concat(third.Items).Select(item => item.WorkflowId).ToList();
        Assert.Equal(expected.Order(), all);

        var clamped = await service.GetWorkQueueAsync(new PagedQuery { Page = 0, PageSize = 500 }, CancellationToken.None);
        Assert.Equal((1, 100, 5), (clamped.Page, clamped.PageSize, clamped.Items.Count));
    }

    [Fact]
    public async Task Supported_sorts_order_rows_and_unknown_sort_falls_back_to_ready_at()
    {
        await using var db = NewDbContext();
        var carrot = await AddPlanAsync(db, "Bravo Farm", "Kandy", "Block 1", "Carrot", null, "Plan.", "WeatherResourceAgent", readyAt: BaseTime.AddHours(2), startDate: new DateOnly(2026, 11, 1));
        var apple = await AddPlanAsync(db, "Charlie Farm", "Kandy", "Block 2", "Apple", null, "Plan.", "WeatherResourceAgent", readyAt: BaseTime.AddHours(3), startDate: new DateOnly(2026, 10, 1));
        var banana = await AddPlanAsync(db, "Alpha Farm", "Kandy", "Block 3", "Banana", null, "Plan.", "WeatherResourceAgent", readyAt: BaseTime.AddHours(1), startDate: new DateOnly(2026, 12, 1));
        var service = NewService(db);

        async Task<Guid[]> Order(string? sortBy, string? direction = null) =>
            (await service.GetWorkQueueAsync(new PagedQuery { SortBy = sortBy, SortDirection = direction }, CancellationToken.None))
            .Items.Select(item => item.WorkflowId).ToArray();

        Assert.Equal([banana.WorkflowId, carrot.WorkflowId, apple.WorkflowId], await Order(null));
        Assert.Equal([banana.WorkflowId, carrot.WorkflowId, apple.WorkflowId], await Order("readyAt"));
        Assert.Equal([apple.WorkflowId, carrot.WorkflowId, banana.WorkflowId], await Order("readyAt", "desc"));
        Assert.Equal([apple.WorkflowId, carrot.WorkflowId, banana.WorkflowId], await Order("preferredStartDate"));
        Assert.Equal([banana.WorkflowId, carrot.WorkflowId, apple.WorkflowId], await Order("PreferredStartDate", "desc"));
        Assert.Equal([banana.WorkflowId, carrot.WorkflowId, apple.WorkflowId], await Order("farmName"));
        Assert.Equal([apple.WorkflowId, banana.WorkflowId, carrot.WorkflowId], await Order("cropName"));
        Assert.Equal([carrot.WorkflowId, banana.WorkflowId, apple.WorkflowId], await Order("cropName", "desc"));
        Assert.Equal([banana.WorkflowId, carrot.WorkflowId, apple.WorkflowId], await Order("budget"));
        Assert.Equal([banana.WorkflowId, carrot.WorkflowId, apple.WorkflowId], await Order("errorMessageSafe"));
    }

    [Fact]
    public async Task Running_analysis_remains_visible_with_its_step_status()
    {
        await using var db = NewDbContext();
        var running = await AddPlanAsync(db, "Green Farm", "Kurunegala", "Block A", "Tomato", null, "Plan.", "WeatherResourceAgent", readyAt: BaseTime);
        var step = await db.AgentSteps.SingleAsync(item => item.Id == running.WeatherStepId);
        step.Status = AgentStepStatus.Running;
        step.StartedAt = BaseTime.AddMinutes(30);
        await db.SaveChangesAsync();

        var result = await NewService(db).GetWorkQueueAsync(new PagedQuery(), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(AgentStepStatus.Running, item.StepStatus);
        Assert.Equal(BaseTime.AddMinutes(30), item.StartedAt);
    }

    [Fact]
    public async Task Ready_at_falls_back_to_the_workflow_timestamp_for_legacy_steps()
    {
        await using var db = NewDbContext();
        var legacy = await AddPlanAsync(db, "Legacy Farm", "Kandy", "Block A", "Tomato", null, "Plan.", "WeatherResourceAgent", readyAt: null);
        var workflow = await db.AgentWorkflows.SingleAsync(item => item.Id == legacy.WorkflowId);

        var item = Assert.Single((await NewService(db).GetWorkQueueAsync(new PagedQuery(), CancellationToken.None)).Items);

        Assert.Equal(workflow.UpdatedAt, item.ReadyAt);
    }

    [Fact]
    public async Task Queue_query_is_read_only()
    {
        await using var db = NewDbContext();
        await AddPlanAsync(db, "Green Farm", "Kurunegala", "Block A", "Tomato", null, "Plan.", "WeatherResourceAgent", readyAt: BaseTime);
        db.ChangeTracker.Clear();
        var workflowsBefore = await db.AgentWorkflows.CountAsync();
        var stepsBefore = await db.AgentSteps.CountAsync();

        await NewService(db).GetWorkQueueAsync(new PagedQuery(), CancellationToken.None);

        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Equal(workflowsBefore, await db.AgentWorkflows.CountAsync());
        Assert.Equal(stepsBefore, await db.AgentSteps.CountAsync());
        Assert.Empty(await db.FarmTasks.ToListAsync());
        Assert.Empty(await db.IrrigationSchedules.ToListAsync());
        Assert.Empty(await db.ApprovalDecisions.ToListAsync());
    }

    private static async Task SeedSearchDataAsync(AppDbContext db)
    {
        await AddPlanAsync(db, "Green Farm", "Kurunegala", "Block A", "Tomato", "Rio Grande", "Plan tomatoes.", "WeatherResourceAgent", readyAt: BaseTime);
        await AddPlanAsync(db, "Paddy Farm", "Polonnaruwa", "Paddy Block North", "Rice", null, "Plant a drought-tolerant crop.", "WeatherResourceAgent", readyAt: BaseTime);
        await AddPlanAsync(db, "Chilli Farm", "Hambantota", "East", "Chilli", null, "Plan peppers.", "WeatherResourceAgent", readyAt: BaseTime);
    }

    private sealed record Plan(Guid RequestId, Guid WorkflowId, Guid WeatherStepId, Guid FieldId);

    private static async Task<Plan> AddPlanAsync(
        AppDbContext db,
        string farmName,
        string location,
        string fieldName,
        string cropName,
        string? varietyName,
        string objective,
        string currentStep,
        DateTime? readyAt,
        DateTime? createdAt = null,
        DateOnly? startDate = null)
    {
        var farmer = new AppUser { FullName = "Farmer", Email = $"farmer.{Guid.NewGuid():N}@example.test", PasswordHash = "hash", Role = ApplicationRole.Farmer, IsActive = true };
        var farm = new Farm { Name = farmName, Location = location, TotalArea = 10, OwnerUser = farmer };
        var field = new Field { Name = fieldName, Area = 1, SoilType = "Loam", Farm = farm, IsActive = true };
        var cropType = new CropType { Name = cropName, IsActive = true };
        var variety = varietyName is null ? null : new CropVariety { CropType = cropType, Name = varietyName, IsActive = true };
        var request = new CropPlanRequest
        {
            Farm = farm,
            Field = field,
            CropType = cropType,
            CropVariety = variety,
            RequestedByUser = farmer,
            PreferredStartDate = startDate ?? new DateOnly(2026, 10, 1),
            PreferredEndDate = new DateOnly(2027, 1, 1),
            Budget = 12000,
            Objective = objective,
            Status = CropPlanRequestStatus.PreliminaryGenerated
        };
        db.AddRange(farmer, farm, field, cropType, request);
        if (variety is not null) db.Add(variety);
        await db.SaveChangesAsync();

        var (workflowId, stepId) = await AddWorkflowAsync(db, request.Id, currentStep, readyAt, createdAt);
        return new Plan(request.Id, workflowId, stepId, field.Id);
    }

    private static async Task<(Guid WorkflowId, Guid WeatherStepId)> AddWorkflowAsync(
        AppDbContext db,
        Guid requestId,
        string currentStep,
        DateTime? readyAt,
        DateTime? createdAt = null)
    {
        var request = await db.CropPlanRequests.SingleAsync(item => item.Id == requestId);
        var timestamp = createdAt ?? BaseTime;
        var fieldDone = currentStep != "CropFieldAnalysisAgent";
        var weatherStep = new AgentStep
        {
            AgentName = "WeatherResourceAgent",
            StepName = "WeatherResourceAnalysis",
            Sequence = 3,
            Status = currentStep == "SchedulingValidationAgent" ? AgentStepStatus.Completed : AgentStepStatus.Pending,
            InputJson = """{"officerNotes":"raw"}""",
            OutputJson = """{"observations":"raw"}"""
        };
        var workflow = new AgentWorkflow
        {
            CropPlanRequestId = request.Id,
            InitiatedByUserId = request.RequestedByUserId,
            Objective = request.Objective,
            Status = AgentWorkflowStatus.Pending,
            CurrentStep = currentStep,
            CandidateRevision = 2,
            Version = 4,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            Steps =
            [
                new AgentStep
                {
                    AgentName = "CropFieldAnalysisAgent",
                    StepName = "FieldAnalysis",
                    Sequence = 2,
                    Status = fieldDone ? AgentStepStatus.Completed : AgentStepStatus.Pending,
                    CompletedAt = readyAt,
                    OutputJson = """{"evidenceInspectionIds":["raw"]}"""
                },
                weatherStep,
                new AgentStep { AgentName = "SchedulingValidationAgent", StepName = "Scheduling", Sequence = 4 }
            ]
        };
        db.Add(workflow);
        await db.SaveChangesAsync();
        return (workflow.Id, weatherStep.Id);
    }

    private static WeatherResourceWorkflowService NewService(AppDbContext db)
    {
        var officer = new StubCurrentUser(ApplicationRole.ResourceOfficer);
        var cropPlanning = new CropPlanningService(
            db,
            officer,
            new FarmRequestValidator(),
            new FieldRequestValidator(),
            new CropTypeRequestValidator(),
            new CropCycleRequestValidator(),
            new CropPlanRequestCreateValidator(),
            new CropPlanRequestUpdateValidator());
        return new WeatherResourceWorkflowService(db, officer, cropPlanning, new UnusedAiClient());
    }

    private sealed class StubCurrentUser(ApplicationRole role) : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public ApplicationRole? Role { get; } = role;
        public bool IsInRole(ApplicationRole roleToCheck) => Role == roleToCheck;
    }

    private sealed class UnusedAiClient : IWeatherResourceAIClient
    {
        public Task<WeatherResourceOutput> RunWeatherResourceAnalysisAsync(WeatherResourceInput input, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The work queue must never run the agent.");
    }
}
