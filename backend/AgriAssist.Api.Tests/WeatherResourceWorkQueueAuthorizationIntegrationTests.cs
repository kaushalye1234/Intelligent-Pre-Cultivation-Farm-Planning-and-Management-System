using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.ExternalServices.AgenticAI;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgriAssist.Api.Tests;

/// <summary>HTTP-level checks for the Resource Officer Weather/Resource work queue and its hand-off to Member 4.</summary>
public sealed class WeatherResourceWorkQueueAuthorizationIntegrationTests
{
    private const string Password = "WorkQueue@2026";
    private const string QueueUrl = "/api/crop-plans/weather-resource-work-queue";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Only_resource_officers_can_read_the_work_queue()
    {
        await using var factory = CreateFactory();
        var data = await SeedAsync(factory);

        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(QueueUrl)).StatusCode);

        foreach (var email in new[] { data.FarmerEmail, data.FieldOfficerEmail, data.AgriculturalOfficerEmail, data.AdminEmail })
        {
            using var client = await ClientForAsync(factory, email);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(QueueUrl)).StatusCode);
        }

        using var resourceOfficer = await ClientForAsync(factory, data.ResourceOfficerEmail);
        var response = await resourceOfficer.GetAsync($"{QueueUrl}?page=1&pageSize=10&search=queue&sortBy=readyAt&sortDirection=asc");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var queue = await response.Content.ReadFromJsonAsync<PagedResult<WeatherResourceWorkItemResponse>>(Json);
        var item = Assert.Single(queue!.Items);
        Assert.Equal(data.CropPlanRequestId, item.CropPlanRequestId);
        Assert.Equal(1, queue.TotalCount);
    }

    [Fact]
    public async Task Queue_json_contains_no_raw_evidence_or_approval_data()
    {
        await using var factory = CreateFactory();
        var data = await SeedAsync(factory);
        using var resourceOfficer = await ClientForAsync(factory, data.ResourceOfficerEmail);

        var json = await resourceOfficer.GetStringAsync(QueueUrl);

        Assert.Contains(data.CropPlanRequestId.ToString(), json, StringComparison.OrdinalIgnoreCase);
        foreach (var forbidden in new[]
                 {
                     "officerNotes", "riskNotes", "risksAndConcerns", "observations", "images", "evidenceInspectionIds",
                     "inputJson", "outputJson", "decisions", "openIssues", "Private staff note", "farmerName", "email"
                 })
        {
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Resource_officer_run_hands_off_to_scheduling_and_leaves_the_queue_without_side_effects()
    {
        await using var factory = CreateFactory();
        var data = await SeedAsync(factory);
        var before = await CountSideEffectsAsync(factory);
        using var resourceOfficer = await ClientForAsync(factory, data.ResourceOfficerEmail);

        var queue = await resourceOfficer.GetFromJsonAsync<PagedResult<WeatherResourceWorkItemResponse>>(QueueUrl, Json);
        var item = Assert.Single(queue!.Items);
        Assert.Equal(AgentStepStatus.Pending, item.StepStatus);

        var handoff = await resourceOfficer.GetFromJsonAsync<Member3HandoffResponse>($"/api/crop-plans/{item.CropPlanRequestId}/member-3-handoff", Json);
        Assert.Equal(item.WorkflowId, handoff!.WorkflowId);

        using var run = await resourceOfficer.PostAsync($"/api/crop-plans/{item.CropPlanRequestId}/run-weather-resource-analysis", null);
        Assert.Equal(HttpStatusCode.OK, run.StatusCode);
        var result = await run.Content.ReadFromJsonAsync<WeatherResourceRunResponse>(Json);
        Assert.Equal("Analyzed", result!.Status);
        Assert.Equal(item.WeatherResourceStepId, result.WeatherResourceStepId);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var workflow = await db.AgentWorkflows.AsNoTracking().Include(entry => entry.Steps).SingleAsync(entry => entry.Id == item.WorkflowId);
            Assert.Equal("SchedulingValidationAgent", workflow.CurrentStep);
            Assert.Equal(AgentWorkflowStatus.Pending, workflow.Status);
            Assert.Equal(AgentStepStatus.Completed, workflow.Steps.Single(step => step.AgentName == "WeatherResourceAgent").Status);
            Assert.Equal(AgentStepStatus.Pending, workflow.Steps.Single(step => step.AgentName == "SchedulingValidationAgent").Status);
        }

        var after = await resourceOfficer.GetFromJsonAsync<PagedResult<WeatherResourceWorkItemResponse>>(QueueUrl, Json);
        Assert.Empty(after!.Items);
        Assert.Equal(0, after.TotalCount);
        Assert.Equal(before, await CountSideEffectsAsync(factory));
    }

    [Fact]
    public async Task Running_item_stays_listed_and_a_duplicate_run_is_rejected()
    {
        await using var factory = CreateFactory();
        var data = await SeedAsync(factory);
        await MutateWorkflowAsync(factory, data.CropPlanRequestId, (workflow, step) =>
        {
            workflow.Status = AgentWorkflowStatus.Running;
            step.Status = AgentStepStatus.Running;
            step.StartedAt = DateTime.UtcNow;
        });
        using var resourceOfficer = await ClientForAsync(factory, data.ResourceOfficerEmail);

        var queue = await resourceOfficer.GetFromJsonAsync<PagedResult<WeatherResourceWorkItemResponse>>(QueueUrl, Json);
        Assert.Equal(AgentStepStatus.Running, Assert.Single(queue!.Items).StepStatus);

        using var run = await resourceOfficer.PostAsync($"/api/crop-plans/{data.CropPlanRequestId}/run-weather-resource-analysis", null);
        Assert.Equal(HttpStatusCode.Conflict, run.StatusCode);
        Assert.Contains("WEATHER_RESOURCE_ALREADY_RUNNING", await run.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal("WeatherResourceAgent", await CurrentStepAsync(factory, data.CropPlanRequestId));
    }

    [Fact]
    public async Task Stale_queue_row_cannot_bypass_the_current_step_check()
    {
        await using var factory = CreateFactory();
        var data = await SeedAsync(factory);
        using var resourceOfficer = await ClientForAsync(factory, data.ResourceOfficerEmail);
        var queue = await resourceOfficer.GetFromJsonAsync<PagedResult<WeatherResourceWorkItemResponse>>(QueueUrl, Json);
        Assert.Single(queue!.Items);

        // Another officer's run ended in a safe failure after this queue was loaded.
        await MutateWorkflowAsync(factory, data.CropPlanRequestId, (workflow, step) =>
        {
            workflow.Status = AgentWorkflowStatus.Failed;
            workflow.CurrentStep = "SafeFailure";
            step.Status = AgentStepStatus.Failed;
        });

        using var run = await resourceOfficer.PostAsync($"/api/crop-plans/{data.CropPlanRequestId}/run-weather-resource-analysis", null);
        Assert.Equal(HttpStatusCode.Conflict, run.StatusCode);
        Assert.Equal("SafeFailure", await CurrentStepAsync(factory, data.CropPlanRequestId));
        var refreshed = await resourceOfficer.GetFromJsonAsync<PagedResult<WeatherResourceWorkItemResponse>>(QueueUrl, Json);
        Assert.Empty(refreshed!.Items);
    }

    [Fact]
    public async Task Existing_run_endpoint_roles_are_unchanged()
    {
        await using var factory = CreateFactory();
        var data = await SeedAsync(factory);

        foreach (var email in new[] { data.FarmerEmail, data.FieldOfficerEmail })
        {
            using var client = await ClientForAsync(factory, email);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/crop-plans/{data.CropPlanRequestId}/run-weather-resource-analysis", null)).StatusCode);
        }

        Assert.Equal("WeatherResourceAgent", await CurrentStepAsync(factory, data.CropPlanRequestId));
    }

    private static async Task<(int Tasks, int Schedules, int Decisions, int Reservations, int Transactions, decimal OnHand, decimal Reserved)> CountSideEffectsAsync(
        WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (
            await db.FarmTasks.CountAsync(),
            await db.IrrigationSchedules.CountAsync(),
            await db.ApprovalDecisions.CountAsync(),
            await db.ResourceReservations.CountAsync(),
            await db.StockTransactions.CountAsync(),
            await db.InventoryStocks.SumAsync(stock => stock.QuantityOnHand),
            await db.InventoryStocks.SumAsync(stock => stock.ReservedQuantity));
    }

    private static async Task MutateWorkflowAsync(WebApplicationFactory<Program> factory, Guid requestId, Action<AgentWorkflow, AgentStep> mutate)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var workflow = await db.AgentWorkflows.Include(item => item.Steps).SingleAsync(item => item.CropPlanRequestId == requestId);
        mutate(workflow, workflow.Steps.Single(step => step.AgentName == "WeatherResourceAgent"));
        await db.SaveChangesAsync();
    }

    private static async Task<string> CurrentStepAsync(WebApplicationFactory<Program> factory, Guid requestId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.AgentWorkflows.Where(item => item.CropPlanRequestId == requestId).Select(item => item.CurrentStep).SingleAsync();
    }

    private static async Task<SeededData> SeedAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var farmer = User(ApplicationRole.Farmer);
        var fieldOfficer = User(ApplicationRole.FieldOfficer);
        var resourceOfficer = User(ApplicationRole.ResourceOfficer);
        var agriculturalOfficer = User(ApplicationRole.AgriculturalOfficer);
        var admin = User(ApplicationRole.Admin);
        var farm = new Farm { Name = "Queue Farm", Location = "North", TotalArea = 10, OwnerUser = farmer };
        var field = new Field { Name = "Queue Field", Area = 2, SoilType = "Loam", Farm = farm, IsActive = true };
        var crop = new CropType { Name = "Rice", IsActive = true };
        var request = new CropPlanRequest
        {
            Farm = farm,
            Field = field,
            CropType = crop,
            RequestedByUser = farmer,
            PreferredStartDate = new DateOnly(2026, 10, 1),
            PreferredEndDate = new DateOnly(2027, 1, 1),
            Budget = 12000,
            Objective = "Verify the Resource Officer queue.",
            Status = CropPlanRequestStatus.PreliminaryGenerated
        };
        var waitingRequest = new CropPlanRequest
        {
            Farm = farm,
            Field = field,
            CropType = crop,
            RequestedByUser = farmer,
            PreferredStartDate = new DateOnly(2027, 2, 1),
            PreferredEndDate = new DateOnly(2027, 5, 1),
            Budget = 9000,
            Objective = "Still waiting for Member 2.",
            Status = CropPlanRequestStatus.PreliminaryGenerated
        };
        var workflow = new AgentWorkflow
        {
            CropPlanRequest = request,
            InitiatedByUser = farmer,
            Objective = request.Objective,
            Status = AgentWorkflowStatus.Pending,
            CurrentStep = "WeatherResourceAgent"
        };
        var fieldAnalysis = new FieldAnalysisOutput(
            workflow.Id,
            "Analyzed",
            true,
            ["Human review remains required."],
            new FieldAnalysisFieldConditionResponse("Structured staff analysis summary.", [Guid.NewGuid()]),
            [new FieldAnalysisOpenIssueResponse(Guid.NewGuid(), "High", "Open", Guid.NewGuid())],
            "High",
            "SuitableWithConditions",
            "Soil type Loamy; condition Moderate; moisture Moist.",
            "Water availability Adequate; main source Canal; irrigation Available; reliability Reliable.",
            "Drainage condition Poor; waterlogging risk Moderate.",
            ["Clear drainage channels before planting."],
            "RequiresPreparation",
            [PrePlantingRisk.PoorDrainage],
            ["Address the drainage concern before planting."]);
        workflow.Steps =
        [
            new AgentStep
            {
                AgentName = "CropFieldAnalysisAgent",
                StepName = "FieldAnalysis",
                Sequence = 2,
                Status = AgentStepStatus.Completed,
                CompletedAt = DateTime.UtcNow.AddMinutes(-5),
                InputJson = """{"officerNotes":"Private staff note"}""",
                OutputJson = JsonSerializer.Serialize(fieldAnalysis, Json)
            },
            new AgentStep { AgentName = "WeatherResourceAgent", StepName = "WeatherResourceAnalysis", Sequence = 3 },
            new AgentStep { AgentName = "SchedulingValidationAgent", StepName = "Scheduling", Sequence = 4 }
        ];
        var waitingWorkflow = new AgentWorkflow
        {
            CropPlanRequest = waitingRequest,
            InitiatedByUser = farmer,
            Objective = waitingRequest.Objective,
            Status = AgentWorkflowStatus.Pending,
            CurrentStep = "CropFieldAnalysisAgent",
            Steps =
            [
                new AgentStep { AgentName = "CropFieldAnalysisAgent", StepName = "FieldAnalysis", Sequence = 2 },
                new AgentStep { AgentName = "WeatherResourceAgent", StepName = "WeatherResourceAnalysis", Sequence = 3 }
            ]
        };
        db.AddRange(farmer, fieldOfficer, resourceOfficer, agriculturalOfficer, admin, farm, field, crop, request, waitingRequest, workflow, waitingWorkflow);
        await db.SaveChangesAsync();

        return new SeededData(farmer.Email, fieldOfficer.Email, resourceOfficer.Email, agriculturalOfficer.Email, admin.Email, request.Id);
    }

    private static AppUser User(ApplicationRole role) =>
        new()
        {
            FullName = $"Test {role}",
            Email = $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@queue.test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
            Role = role,
            IsActive = true,
            MustChangePassword = false,
            PasswordChangedAt = DateTime.UtcNow,
            TokenVersion = 1
        };

    private static async Task<HttpClient> ClientForAsync(WebApplicationFactory<Program> factory, string email)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        Assert.True(login.IsSuccessStatusCode, await login.Content.ReadAsStringAsync());
        var session = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        return client;
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Jwt:Secret"] = "test-jwt-secret-with-at-least-32-chars",
                        ["Jwt:Issuer"] = "AgriAssist",
                        ["Jwt:Audience"] = "AgriAssistUsers",
                        ["Jwt:ExpiryMinutes"] = "60",
                        ["Jwt:PasswordChangeExpiryMinutes"] = "10",
                        ["Security:RateLimits:Login:PermitLimit"] = "100"
                    });
                });
                builder.ConfigureTestServices(services => services.AddScoped<IWeatherResourceAIClient, EvidenceRecordingAgent>());
            });

    /// <summary>
    /// Stands in for the Python agent: records the read-only availability tool call the way the internal tools
    /// controller does, then returns the only output that evidence supports (no forecast, no verified requirements).
    /// </summary>
    private sealed class EvidenceRecordingAgent(AppDbContext db) : IWeatherResourceAIClient
    {
        public async Task<WeatherResourceOutput> RunWeatherResourceAnalysisAsync(WeatherResourceInput input, CancellationToken cancellationToken)
        {
            db.AgentToolExecutions.Add(new AgentToolExecution
            {
                AgentStepId = input.AgentStepId,
                ToolName = WeatherResourceToolNames.GetResourceAvailability,
                InputJson = JsonSerializer.Serialize(new { resourceIds = Array.Empty<Guid>(), workflowId = input.WorkflowId, agentStepId = input.AgentStepId }, Json),
                OutputJson = "[]",
                Status = AgentToolExecutionStatus.Completed
            });
            await db.SaveChangesAsync(cancellationToken);
            return new WeatherResourceOutput(input.WorkflowId, "Analyzed", true, ["Forecast unavailable; human review required."], "Unknown",
                "No forecast was available.", [], ["Confirm resources before planting."], [], ResourceRequirementStatus.Unknown);
        }
    }

    private sealed record SeededData(
        string FarmerEmail,
        string FieldOfficerEmail,
        string ResourceOfficerEmail,
        string AgriculturalOfficerEmail,
        string AdminEmail,
        Guid CropPlanRequestId);
}
