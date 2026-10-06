using System.Security.Cryptography;
using System.Text.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Dtos.TaskApproval;
using AgriAssist.Api.ExternalServices.AgenticAI;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Models.TaskApproval;
using AgriAssist.Api.Services.Resources;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Services.TaskApproval;
using AgriAssist.Api.Validators.Resources;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? throw new InvalidOperationException("Set ConnectionStrings__DefaultConnection for a disposable local database.");
var target = new NpgsqlConnectionStringBuilder(connectionString);
if (target.Host is not ("localhost" or "127.0.0.1") || target.Port != 55432 ||
    string.IsNullOrWhiteSpace(target.Database) ||
    !target.Database.StartsWith("agriassist_member4_demo_", StringComparison.Ordinal))
{
    throw new InvalidOperationException("This fixture runner only accepts a local database named agriassist_member4_demo_* on port 55432.");
}

var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
await using var db = new AppDbContext(options);
if (!await db.Database.CanConnectAsync() || (await db.Database.GetPendingMigrationsAsync()).Any())
    throw new InvalidOperationException("The disposable database must exist and have all migrations applied.");

var run = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
var scenarios = new[]
{
    new Scenario("Approve candidate", AgentWorkflowStatus.PendingOfficerApproval, false, false),
    new Scenario("Reject candidate", AgentWorkflowStatus.PendingOfficerApproval, false, false),
    new Scenario("Request revision", AgentWorkflowStatus.PendingOfficerApproval, false, false),
    new Scenario("Missing weather evidence", AgentWorkflowStatus.MissingDependency, true, false),
    new Scenario("Conflicting task", AgentWorkflowStatus.Failed, false, true)
};
if (args.Length > 1 || (args.Length == 1 && !scenarios.Any(item => item.Name.Equals(args[0], StringComparison.OrdinalIgnoreCase))))
    throw new ArgumentException("Pass no scenario for all five cases, or one exact scenario name to add only that case.");

var password = args.Length == 0 ? "Demo!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8)) : null;
var officer = args.Length == 0
    ? new AppUser
    {
        FullName = "Member 4 Demo Officer",
        Email = $"member4-officer-{run}@example.test",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password!),
        Role = ApplicationRole.AgriculturalOfficer,
        IsActive = true
    }
    : await db.Users.SingleAsync(user => user.Email.StartsWith("member4-officer-") && user.Email.EndsWith("@example.test") && user.Role == ApplicationRole.AgriculturalOfficer);
if (args.Length == 0)
{
    db.Users.Add(officer);
    await db.SaveChangesAsync();
}

var selectedScenarios = args.Length == 0 ? scenarios : scenarios.Where(item => item.Name.Equals(args[0], StringComparison.OrdinalIgnoreCase)).ToArray();

foreach (var (scenario, index) in selectedScenarios.Select((item, index) => (item, index)))
{
    var farmer = new AppUser
    {
        FullName = $"Member 4 Demo Farmer {index + 1}",
        Email = $"member4-farmer-{run}-{index + 1}@example.test",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(16))),
        Role = ApplicationRole.Farmer,
        IsActive = true
    };
    var farm = new Farm { Name = $"Member 4 Demo Farm {index + 1}", Location = "Kurunegala", TotalArea = 10, OwnerUser = farmer };
    var field = new Field { Name = $"Demo Field {index + 1}", Area = 2, SoilType = "Loam", Farm = farm, IsActive = true };
    var crop = new CropType { Name = $"Demo Rice {run}-{index + 1}", IsActive = true };
    var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14 + index * 10));
    var plan = new CropPlanRequest
    {
        Farm = farm,
        Field = field,
        CropType = crop,
        RequestedByUser = farmer,
        PreferredStartDate = start,
        PreferredEndDate = start.AddDays(7),
        Budget = 12000,
        Objective = $"MEMBER 4 DEMO {run}: {scenario.Name}",
        Status = CropPlanRequestStatus.PreliminaryGenerated
    };
    var workflow = new AgentWorkflow
    {
        CropPlanRequest = plan,
        InitiatedByUser = farmer,
        Objective = plan.Objective,
        Status = AgentWorkflowStatus.Pending,
        CurrentStep = "SchedulingValidationAgent"
    };
    workflow.Steps =
    [
        new AgentStep { AgentName = "CropPlanningCoordinatorAgent", StepName = "CropPlanningCoordinator", Sequence = 1, Status = AgentStepStatus.Completed,
            OutputJson = JsonSerializer.Serialize(new { workflowId = workflow.Id, status = "Planned", referenceDataStatus = "SyntheticDemo", warnings = new[] { "Synthetic demonstration evidence only." } }) },
        new AgentStep { AgentName = "CropFieldAnalysisAgent", StepName = "FieldAnalysis", Sequence = 2, Status = AgentStepStatus.Completed,
            OutputJson = JsonSerializer.Serialize(new { workflowId = workflow.Id, status = "Analyzed", priority = "High", warnings = new[] { "Synthetic demonstration evidence only." } }) },
        new AgentStep { AgentName = "WeatherResourceAgent", StepName = "WeatherResourceAnalysis", Sequence = 3,
            Status = scenario.MissingWeather ? AgentStepStatus.Pending : AgentStepStatus.Completed,
            OutputJson = scenario.MissingWeather ? "{}" : JsonSerializer.Serialize(new { workflowId = workflow.Id, status = "Analyzed", weatherRisk = "Medium", warnings = new[] { "Synthetic demonstration evidence only." } }) },
        new AgentStep { AgentName = "SchedulingValidationAgent", StepName = "Scheduling", Sequence = 4, Status = AgentStepStatus.Pending }
    ];
    db.AddRange(farmer, farm, field, crop, plan, workflow);
    if (scenario.ConflictingTask)
    {
        db.FarmTasks.Add(new FarmTask
        {
            Farm = farm,
            AssignedToUser = farmer,
            DueAt = AtUtc(start, 8),
            Title = "Existing demo task",
            Status = FarmTaskStatus.PendingApproval
        });
    }
    await db.SaveChangesAsync();

    var currentUser = new DemoCurrentUser(officer.Id);
    var resources = new ResourceService(db, currentUser,
        new ResourceCategoryRequestValidator(), new SupplierRequestValidator(),
        new ResourceRequestValidator(), new InventoryStockRequestValidator(),
        new ResourceReservationRequestValidator());
    var ai = new DemoSchedulingClient(scenario.MissingWeather);
    var approval = new WorkflowApprovalService(db, currentUser, ai, resources);
    var review = await approval.GenerateCandidateAsync(workflow.Id, CancellationToken.None);
    if (review.Workflow.Status != scenario.ExpectedStatus)
        throw new InvalidOperationException($"{scenario.Name}: expected {scenario.ExpectedStatus}, got {review.Workflow.Status}.");
    Console.WriteLine($"CASE|{scenario.Name}|{workflow.Id}|{review.Workflow.Status}");
}

if (password is not null) Console.WriteLine($"LOGIN|{officer.Email}|{password}");
else Console.WriteLine($"OFFICER|{officer.Email}|existing password unchanged");
Console.WriteLine($"DATABASE|{target.Database}");

static DateTime AtUtc(DateOnly date, int hour) =>
    DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(hour, 0)), DateTimeKind.Utc);

internal sealed record Scenario(string Name, AgentWorkflowStatus ExpectedStatus, bool MissingWeather, bool ConflictingTask);

internal sealed class DemoCurrentUser(Guid userId) : ICurrentUserService
{
    public Guid? UserId { get; } = userId;
    public ApplicationRole? Role => ApplicationRole.AgriculturalOfficer;
    public bool IsInRole(ApplicationRole role) => Role == role;
}

internal sealed class DemoSchedulingClient(bool missingWeather) : ISchedulingValidationAIClient
{
    public Task<SchedulingValidationOutput> RunSchedulingValidationAsync(SchedulingValidationInput input, CancellationToken cancellationToken)
    {
        if (missingWeather)
        {
            return Task.FromResult(new SchedulingValidationOutput(
                input.WorkflowId, input.CandidateRevision, "MissingDependency", true, false,
                ["Weather/resource output is missing in this synthetic demo."], [], [], [], null,
                [new SchedulingConstraint("UPSTREAM_DEPENDENCY", "Blocking", "Weather/resource output is missing.")]));
        }
        return Task.FromResult(new SchedulingValidationOutput(
            input.WorkflowId, input.CandidateRevision, "CandidateReady", true, true,
            ["Synthetic demonstration candidate. Officer review is required."],
            [new SchedulingCandidateTask(input.FarmId, "Demo: review field readiness", "Synthetic demonstration task.", AtUtc(input.PreferredStartDate, 8), input.AssignedToUserId)],
            [new SchedulingCandidateIrrigation(input.FieldId!.Value, AtUtc(input.PreferredStartDate.AddDays(1), 6), 60, "Synthetic demonstration irrigation.")],
            [], null,
            [new SchedulingConstraint("HUMAN_APPROVAL", "Blocking", "Officer approval is required.")]));
    }

    private static DateTime AtUtc(DateOnly date, int hour) =>
        DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(hour, 0)), DateTimeKind.Utc);
}
