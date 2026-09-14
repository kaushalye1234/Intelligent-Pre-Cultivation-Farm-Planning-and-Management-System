using System.Text.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.ExternalServices.AgenticAI;
using AgriAssist.Api.ExternalServices.Weather;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Resources;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.CropPlanning;
using AgriAssist.Api.Services.Resources;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.CropPlanning;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Tests;

public sealed class WeatherResourceWorkflowTests
{
    [Fact]
    public async Task Run_stores_output_and_hands_off_to_member4()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, fieldAnalysisDone: true);
        WeatherResourceInput? sentInput = null;
        var service = NewService(db, new FakeAiClient(input => { sentInput = input; return Echo(input, "Medium"); }));

        var result = await service.RunAsync(data.RequestId, CancellationToken.None);

        Assert.Equal("Analyzed", result.Status);
        Assert.Equal("Medium", result.WeatherRisk);
        Assert.NotNull(sentInput);
        Assert.Equal("Kurunegala", sentInput.Location);
        Assert.Equal("High", sentInput.FieldPriority);
        Assert.Equal(6, sentInput.Stocks.Single().AvailableQuantity);

        var workflow = await db.AgentWorkflows.Include(item => item.Steps).SingleAsync();
        Assert.Equal(AgentWorkflowStatus.Pending, workflow.Status);
        Assert.Equal("SchedulingValidationAgent", workflow.CurrentStep);
        Assert.Equal(AgentStepStatus.Completed, workflow.Steps.Single(step => step.AgentName == "WeatherResourceAgent").Status);

        var stored = await service.GetResultAsync(data.RequestId, CancellationToken.None);
        Assert.Equal("Medium", stored.WeatherRisk);
        Assert.Equal(data.StockId, stored.ResourceChecks.Single().InventoryStockId);
    }

    [Fact]
    public async Task Run_requires_field_analysis_to_be_completed_first()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, fieldAnalysisDone: false);
        var service = NewService(db, new FakeAiClient(input => Echo(input, "Low")));

        var exception = await Assert.ThrowsAsync<ApiException>(() => service.RunAsync(data.RequestId, CancellationToken.None));

        Assert.Equal("FIELD_ANALYSIS_NOT_COMPLETED", exception.Code);
    }

    [Fact]
    public async Task Run_rejects_invented_stock_figures()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, fieldAnalysisDone: true);
        var service = NewService(db, new FakeAiClient(input =>
        {
            var output = Echo(input, "Low");
            var check = output.ResourceChecks.Single();
            return output with { ResourceChecks = [check with { AvailableQuantity = 500 }] };
        }));

        var result = await service.RunAsync(data.RequestId, CancellationToken.None);

        Assert.Equal("SafeFailure", result.Status);
        var workflow = await db.AgentWorkflows.Include(item => item.Steps).SingleAsync();
        Assert.Equal(AgentWorkflowStatus.Failed, workflow.Status);
        Assert.Equal("AI_RESPONSE_INVALID", workflow.Steps.Single(step => step.AgentName == "WeatherResourceAgent").ErrorCode);
    }

    [Fact]
    public async Task Run_records_safe_failure_when_ai_service_is_down()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, fieldAnalysisDone: true);
        var service = NewService(db, new FakeAiClient(_ => throw new HttpRequestException("No service.")));

        var result = await service.RunAsync(data.RequestId, CancellationToken.None);

        Assert.Equal("SafeFailure", result.Status);
        Assert.True(result.RequiresHumanReview);
        Assert.Equal("AI_SERVICE_UNAVAILABLE", (await db.AgentSteps.SingleAsync(step => step.AgentName == "WeatherResourceAgent")).ErrorCode);
    }

    [Fact]
    public void Validate_rejects_weather_risk_without_forecast()
    {
        var input = new WeatherResourceInput(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Nowhere", new DateOnly(2026, 10, 1), new DateOnly(2027, 1, 1), "Low", "", WeatherForecastResponse.Unavailable("Nowhere", "down"), []);
        var output = new WeatherResourceOutput(input.WorkflowId, "Analyzed", true, [], "High", "Invented storm.", [], []);

        var errors = WeatherResourceWorkflowService.Validate(output, input);

        Assert.Contains(errors, error => error.Contains("without forecast data"));
    }

    [Fact]
    public void Weather_parser_groups_three_hour_forecasts_into_days()
    {
        const string json = """
        {"list":[
          {"dt":1789977600,"main":{"temp_min":24.1,"temp_max":29.3},"rain":{"3h":2.5},"wind":{"speed":4.2},"weather":[{"description":"light rain"}]},
          {"dt":1789988400,"main":{"temp_min":23.0,"temp_max":31.8},"rain":{"3h":1.5},"wind":{"speed":6.0},"weather":[{"description":"light rain"}]},
          {"dt":1790064000,"main":{"temp_min":22.0,"temp_max":28.0},"wind":{"speed":3.0},"weather":[{"description":"clear sky"}]}
        ]}
        """;

        using var document = JsonDocument.Parse(json);
        var days = WeatherService.ParseDays(document.RootElement);

        Assert.Equal(2, days.Count);
        Assert.Equal(4.0m, days[0].RainMm);
        Assert.Equal(31.8m, days[0].MaxTemperatureC);
        Assert.Equal(23.0m, days[0].MinTemperatureC);
        Assert.Equal("light rain", days[0].Description);
        Assert.Equal(0m, days[1].RainMm);
    }

    private static WeatherResourceOutput Echo(WeatherResourceInput input, string risk) =>
        new(
            input.WorkflowId,
            "Analyzed",
            false,
            [],
            risk,
            "Summary from stored forecast.",
            input.Stocks.Select(stock => new ResourceCheckResponse(stock.InventoryStockId, stock.ResourceId, stock.ResourceName, stock.Unit, stock.AvailableQuantity, stock.AvailableQuantity <= stock.LowStockThreshold)).ToList(),
            ["Review stock before planting."]);

    private static AppDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static WeatherResourceWorkflowService NewService(AppDbContext db, IWeatherResourceAIClient aiClient)
    {
        var officer = new StubCurrentUser(ApplicationRole.AgriculturalOfficer);
        var cropPlanning = new CropPlanningService(
            db,
            officer,
            new FarmRequestValidator(),
            new FieldRequestValidator(),
            new CropTypeRequestValidator(),
            new CropCycleRequestValidator(),
            new CropPlanRequestCreateValidator(),
            new CropPlanRequestUpdateValidator());
        return new WeatherResourceWorkflowService(db, officer, cropPlanning, new StubWeatherService(), aiClient);
    }

    private static async Task<(Guid RequestId, Guid StockId)> SeedAsync(AppDbContext db, bool fieldAnalysisDone)
    {
        var farmer = new AppUser { FullName = "Farmer", Email = "farmer.m3@example.test", PasswordHash = "hash", Role = ApplicationRole.Farmer, IsActive = true };
        var farm = new Farm { Name = "North Farm", Location = "Kurunegala", TotalArea = 10, OwnerUser = farmer };
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
            Status = CropPlanRequestStatus.PreliminaryGenerated
        };
        var fieldAnalysis = new FieldAnalysisOutput(Guid.Empty, "Analyzed", true, [], new FieldAnalysisFieldConditionResponse("Yellowing near low area.", []), [], "High");
        var workflow = new AgentWorkflow
        {
            CropPlanRequest = request,
            InitiatedByUser = farmer,
            Objective = request.Objective,
            Status = AgentWorkflowStatus.Pending,
            CurrentStep = fieldAnalysisDone ? "WeatherResourceAgent" : "CropFieldAnalysisAgent",
            Steps =
            [
                new AgentStep { AgentName = "CropFieldAnalysisAgent", StepName = "FieldAnalysis", Sequence = 2, Status = fieldAnalysisDone ? AgentStepStatus.Completed : AgentStepStatus.Pending, OutputJson = JsonSerializer.Serialize(fieldAnalysis, new JsonSerializerOptions(JsonSerializerDefaults.Web)) },
                new AgentStep { AgentName = "WeatherResourceAgent", StepName = "WeatherResourceAnalysis", Sequence = 3 },
                new AgentStep { AgentName = "SchedulingValidationAgent", StepName = "Scheduling", Sequence = 4 }
            ]
        };
        var stock = new InventoryStock
        {
            Resource = new Resource { Name = "Paddy Seed", Unit = "kg", ResourceCategory = new ResourceCategory { Name = "Seed" } },
            QuantityOnHand = 10,
            ReservedQuantity = 4,
            LowStockThreshold = 8
        };
        db.AddRange(farmer, farm, field, cropType, request, workflow, stock);
        await db.SaveChangesAsync();
        return (request.Id, stock.Id);
    }

    private sealed class StubCurrentUser(ApplicationRole role) : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public ApplicationRole? Role { get; } = role;
        public bool IsInRole(ApplicationRole roleToCheck) => Role == roleToCheck;
    }

    private sealed class StubWeatherService : IWeatherService
    {
        public Task<WeatherForecastResponse> GetForecastAsync(string location, CancellationToken cancellationToken) =>
            Task.FromResult(new WeatherForecastResponse(location, true, "stub", [new WeatherDayResponse(new DateOnly(2026, 9, 15), 23, 31, 12, 5, "moderate rain")]));
    }

    private sealed class FakeAiClient(Func<WeatherResourceInput, WeatherResourceOutput> respond) : IWeatherResourceAIClient
    {
        public Task<WeatherResourceOutput> RunWeatherResourceAnalysisAsync(WeatherResourceInput input, CancellationToken cancellationToken) =>
            Task.FromResult(respond(input));
    }
}
