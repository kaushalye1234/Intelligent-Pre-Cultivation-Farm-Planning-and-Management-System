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
        Assert.NotNull(sentInput.Member2FieldAnalysisContext);
        Assert.Equal("SuitableWithConditions", sentInput.Member2FieldAnalysisContext.FieldSuitability);
        Assert.Contains("Adequate", sentInput.Member2FieldAnalysisContext.WaterAssessment);
        Assert.Contains("Poor", sentInput.Member2FieldAnalysisContext.DrainageAssessment);
        Assert.Equal("RequiresPreparation", sentInput.Member2FieldAnalysisContext.PlantingReadiness);
        Assert.Equal([PrePlantingRisk.PoorDrainage], sentInput.Member2FieldAnalysisContext.IdentifiedRisks);
        Assert.NotEmpty(sentInput.Member2FieldAnalysisContext.FieldPreparationRequirements);
        var inputJson = JsonSerializer.Serialize(sentInput, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("officerNotes", inputJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evidenceInspectionIds", inputJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("openIssues", inputJson, StringComparison.OrdinalIgnoreCase);

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
    public async Task Run_marks_requirements_unknown_because_crop_planning_provides_no_quantities()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, fieldAnalysisDone: true);
        WeatherResourceInput? sentInput = null;
        var service = NewService(db, new FakeAiClient(input => { sentInput = input; return Echo(input, "Low"); }));

        var result = await service.RunAsync(data.RequestId, CancellationToken.None);

        Assert.Equal("Analyzed", result.Status);
        Assert.NotNull(sentInput);
        Assert.Empty(sentInput!.ResourceRequirements!);
        var stored = await service.GetResultAsync(data.RequestId, CancellationToken.None);
        var check = Assert.Single(stored.ResourceChecks);
        Assert.Null(check.Requested);
        Assert.Null(check.Sufficient);
        Assert.Equal(ResourceRequirementStatus.Unknown, check.RequirementStatus);
    }

    [Fact]
    public void Validate_accepts_requirement_figures_that_follow_from_the_input()
    {
        var (input, stock) = InputWithStock(requestedQuantity: 8m);
        var sufficient = OutputWith(input, new ResourceCheckResponse(stock.InventoryStockId, stock.ResourceId, stock.ResourceName, stock.Unit, 10m, false, 8m, true, ResourceRequirementStatus.Sufficient));
        var insufficientInput = input with { ResourceRequirements = [new ResourceRequirement(stock.ResourceId, 12m)] };
        var insufficient = OutputWith(insufficientInput, new ResourceCheckResponse(stock.InventoryStockId, stock.ResourceId, stock.ResourceName, stock.Unit, 10m, false, 12m, false, ResourceRequirementStatus.Insufficient));

        Assert.Empty(WeatherResourceWorkflowService.Validate(sufficient, input));
        Assert.Empty(WeatherResourceWorkflowService.Validate(insufficient, insufficientInput));
    }

    [Fact]
    public void Validate_rejects_invented_or_inconsistent_requirements()
    {
        var (input, stock) = InputWithStock(requestedQuantity: null);
        var invented = OutputWith(input, new ResourceCheckResponse(stock.InventoryStockId, stock.ResourceId, stock.ResourceName, stock.Unit, 10m, false, 5m, true, ResourceRequirementStatus.Sufficient));
        Assert.Contains(WeatherResourceWorkflowService.Validate(invented, input), error => error.Contains("invented a requirement"));

        var withRequirement = input with { ResourceRequirements = [new ResourceRequirement(stock.ResourceId, 12m)] };
        var wrongQuantity = OutputWith(withRequirement, new ResourceCheckResponse(stock.InventoryStockId, stock.ResourceId, stock.ResourceName, stock.Unit, 10m, false, 5m, true, ResourceRequirementStatus.Sufficient));
        var wrongSufficiency = OutputWith(withRequirement, new ResourceCheckResponse(stock.InventoryStockId, stock.ResourceId, stock.ResourceName, stock.Unit, 10m, false, 12m, true, ResourceRequirementStatus.Sufficient));
        var droppedRequirement = OutputWith(withRequirement, new ResourceCheckResponse(stock.InventoryStockId, stock.ResourceId, stock.ResourceName, stock.Unit, 10m, false));
        Assert.Contains(WeatherResourceWorkflowService.Validate(wrongQuantity, withRequirement), error => error.Contains("requirement figures"));
        Assert.Contains(WeatherResourceWorkflowService.Validate(wrongSufficiency, withRequirement), error => error.Contains("requirement figures"));
        Assert.Contains(WeatherResourceWorkflowService.Validate(droppedRequirement, withRequirement), error => error.Contains("requirement figures"));
    }

    private static (WeatherResourceInput Input, StockSnapshot Stock) InputWithStock(decimal? requestedQuantity)
    {
        var stock = new StockSnapshot(Guid.NewGuid(), Guid.NewGuid(), "Paddy Seed", "kg", 10m, 0m, 10m, 2m);
        var requirements = requestedQuantity is null ? [] : new[] { new ResourceRequirement(stock.ResourceId, requestedQuantity.Value) };
        var forecast = new WeatherForecastResponse("Kurunegala", true, "ok", [new WeatherDayResponse(new DateOnly(2026, 9, 15), 23m, 31m, 2m, 5m, "clear")]);
        var input = new WeatherResourceInput(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Kurunegala", new DateOnly(2026, 10, 1), new DateOnly(2027, 1, 1), "Low", "", forecast, [stock], requirements);
        return (input, stock);
    }

    private static WeatherResourceOutput OutputWith(WeatherResourceInput input, ResourceCheckResponse check) =>
        new(input.WorkflowId, "Analyzed", false, [], "Low", "Summary.", [check], []);

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
        var workflow = new AgentWorkflow
        {
            CropPlanRequest = request,
            InitiatedByUser = farmer,
            Objective = request.Objective,
            Status = AgentWorkflowStatus.Pending,
            CurrentStep = fieldAnalysisDone ? "WeatherResourceAgent" : "CropFieldAnalysisAgent"
        };
        var fieldAnalysis = new FieldAnalysisOutput(
            workflow.Id,
            "Analyzed",
            true,
            ["Field preparation requires Resource Officer awareness."],
            new FieldAnalysisFieldConditionResponse("Submitted pre-planting evidence requires drainage preparation.", [Guid.NewGuid()]),
            [new FieldAnalysisOpenIssueResponse(Guid.NewGuid(), "High", "Open", Guid.NewGuid())],
            "High",
            "SuitableWithConditions",
            "Soil type Loamy; condition Moderate; moisture Moist.",
            "Water availability Adequate; main source Canal; irrigation Available; reliability Reliable.",
            "Drainage condition Poor; waterlogging risk Moderate.",
            ["Clear the recorded drainage channels before planting."],
            "RequiresPreparation",
            [PrePlantingRisk.PoorDrainage],
            ["Address the recorded drainage concern before planting."]);
        workflow.Steps =
        [
            new AgentStep { AgentName = "CropFieldAnalysisAgent", StepName = "FieldAnalysis", Sequence = 2, Status = fieldAnalysisDone ? AgentStepStatus.Completed : AgentStepStatus.Pending, OutputJson = JsonSerializer.Serialize(fieldAnalysis, new JsonSerializerOptions(JsonSerializerDefaults.Web)) },
            new AgentStep { AgentName = "WeatherResourceAgent", StepName = "WeatherResourceAnalysis", Sequence = 3 },
            new AgentStep { AgentName = "SchedulingValidationAgent", StepName = "Scheduling", Sequence = 4 }
        ];
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
