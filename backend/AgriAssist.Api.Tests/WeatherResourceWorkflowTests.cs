using System.Text.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Dtos.Inspections;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.ExternalServices.AgenticAI;
using AgriAssist.Api.ExternalServices.Weather;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.CropPlanning;
using AgriAssist.Api.Services.Resources;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.CropPlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using static AgriAssist.Api.Tests.WeatherResourceTestData;

namespace AgriAssist.Api.Tests;

public sealed class WeatherResourceWorkflowTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Run_stores_output_and_hands_off_to_member4()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, farmLocation: "Wariyapola", farmDistrict: "Kurunegala");
        var agent = new ToolCallingAgent(db);

        var result = await NewService(db, agent).RunAsync(data.RequestId, CancellationToken.None);

        Assert.Equal("Analyzed", result.Status);
        Assert.Equal("Medium", result.WeatherRisk);
        Assert.Equal(ResourceRequirementStatus.Insufficient, result.RequirementStatus);
        var sentInput = agent.Input!;
        Assert.Equal("Wariyapola, Kurunegala, Sri Lanka", sentInput.Location);
        Assert.Equal(data.FieldId, sentInput.FieldId);
        Assert.Equal("High", sentInput.FieldPriority);
        Assert.NotNull(sentInput.Member2FieldAnalysisContext);
        Assert.Equal("SuitableWithConditions", sentInput.Member2FieldAnalysisContext.FieldSuitability);
        Assert.Equal([PrePlantingRisk.PoorDrainage], sentInput.Member2FieldAnalysisContext.IdentifiedRisks);
        var inputJson = JsonSerializer.Serialize(sentInput, Json);
        Assert.DoesNotContain("officerNotes", inputJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evidenceInspectionIds", inputJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("openIssues", inputJson, StringComparison.OrdinalIgnoreCase);

        var workflow = await db.AgentWorkflows.Include(item => item.Steps).SingleAsync();
        Assert.Equal(AgentWorkflowStatus.Pending, workflow.Status);
        Assert.Equal("SchedulingValidationAgent", workflow.CurrentStep);
        Assert.Equal(AgentStepStatus.Completed, workflow.Steps.Single(step => step.AgentName == "WeatherResourceAgent").Status);

        // Member 4 reads this stored output: 50 kg required (100 kg/acre x 0.5 acre), 30 kg available after 40 kg reserved.
        var stored = await NewService(db, agent).GetResultAsync(data.RequestId, CancellationToken.None);
        var requirement = Assert.Single(stored.ResourceRequirements!);
        Assert.Equal((50m, 30m, 40m, 20m), (requirement.RequiredQuantity!.Value, requirement.AvailableQuantity!.Value, requirement.ReservedQuantity!.Value, requirement.ShortageQuantity!.Value));
        Assert.Equal(ResourceRequirementStatus.Insufficient, requirement.RequirementStatus);
        Assert.Equal(data.StockId, stored.ResourceChecks.Single().InventoryStockId);
        Assert.Equal(50m, stored.ResourceChecks.Single().Requested);
        Assert.Equal("SAMPLE source (test fixture)", stored.RequirementSource!.SourceName);
    }

    [Fact]
    public async Task Run_reports_sufficient_when_verified_requirement_is_covered()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, onHand: 60, reserved: 0);

        var result = await NewService(db, new ToolCallingAgent(db)).RunAsync(data.RequestId, CancellationToken.None);

        Assert.Equal(ResourceRequirementStatus.Sufficient, result.RequirementStatus);
        Assert.Equal("Analyzed", result.Status);
    }

    [Fact]
    public async Task Run_marks_requirements_unknown_when_no_verified_rule_exists()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, ruleJson: null);

        var result = await NewService(db, new ToolCallingAgent(db)).RunAsync(data.RequestId, CancellationToken.None);

        Assert.Equal("Analyzed", result.Status);
        Assert.Equal(ResourceRequirementStatus.Unknown, result.RequirementStatus);
        var stored = await NewService(db, new ToolCallingAgent(db)).GetResultAsync(data.RequestId, CancellationToken.None);
        var check = Assert.Single(stored.ResourceChecks);
        Assert.Null(check.Requested);
        Assert.Null(check.Sufficient);
        Assert.Equal(ResourceRequirementStatus.Unknown, check.RequirementStatus);
        Assert.All(stored.ResourceRequirements!, item => Assert.Null(item.RequiredQuantity));
    }

    [Fact]
    public async Task Run_requires_field_analysis_to_be_completed_first()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, fieldAnalysisDone: false);

        var exception = await Assert.ThrowsAsync<ApiException>(() => NewService(db, new ToolCallingAgent(db)).RunAsync(data.RequestId, CancellationToken.None));

        Assert.Equal("FIELD_ANALYSIS_NOT_COMPLETED", exception.Code);
    }

    [Fact]
    public async Task Run_rejects_invented_stock_figures()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db);
        var agent = new ToolCallingAgent(db, output => output with { ResourceChecks = [output.ResourceChecks.Single() with { AvailableQuantity = 500 }] });

        var result = await NewService(db, agent).RunAsync(data.RequestId, CancellationToken.None);

        Assert.Equal("SafeFailure", result.Status);
        var workflow = await db.AgentWorkflows.Include(item => item.Steps).SingleAsync();
        Assert.Equal(AgentWorkflowStatus.Failed, workflow.Status);
        Assert.Equal("AI_RESPONSE_INVALID", workflow.Steps.Single(step => step.AgentName == "WeatherResourceAgent").ErrorCode);
    }

    [Fact]
    public async Task Run_rejects_a_requirement_that_no_verified_rule_supports()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, ruleJson: null);
        var agent = new ToolCallingAgent(db, output => output with
        {
            ResourceRequirements = [new ResourceRequirementAssessment(null, data.ResourceId, "Urea", "kg", 80, 30, 40, 50, false, ResourceRequirementStatus.Insufficient, "Probably 80 kg.", null)],
            RequirementStatus = ResourceRequirementStatus.Insufficient
        });

        var result = await NewService(db, agent).RunAsync(data.RequestId, CancellationToken.None);

        Assert.Equal("SafeFailure", result.Status);
        var errors = await db.AgentValidationResults.Select(item => item.ErrorsJson).SingleAsync();
        Assert.Contains("no verified rule supports it", errors);
    }

    [Fact]
    public async Task Run_rejects_a_dropped_shortage_or_altered_requirement_figures()
    {
        foreach (var tamper in new Func<WeatherResourceOutput, WeatherResourceOutput>[]
                 {
                     output => output with { ResourceRequirements = [], RequirementStatus = ResourceRequirementStatus.Sufficient, RequiresHumanReview = false },
                     output => output with { ResourceRequirements = [output.ResourceRequirements!.Single() with { RequiredQuantity = 25, ShortageQuantity = 0, Sufficient = true, RequirementStatus = ResourceRequirementStatus.Sufficient }] },
                     output => output with { ResourceRequirements = [output.ResourceRequirements!.Single() with { AvailableQuantity = 70, ShortageQuantity = 0 }] }
                 })
        {
            await using var db = NewDbContext();
            var data = await SeedAsync(db);

            var result = await NewService(db, new ToolCallingAgent(db, tamper)).RunAsync(data.RequestId, CancellationToken.None);

            Assert.Equal("SafeFailure", result.Status);
        }
    }

    [Fact]
    public async Task Run_rejects_resource_figures_that_were_not_retrieved_through_tools()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db);

        var result = await NewService(db, new ToolCallingAgent(db, callTools: false)).RunAsync(data.RequestId, CancellationToken.None);

        Assert.Equal("SafeFailure", result.Status);
        var errors = await db.AgentValidationResults.Select(item => item.ErrorsJson).SingleAsync();
        Assert.Contains("GetResourceAvailability", errors);
    }

    [Fact]
    public async Task Run_records_safe_failure_when_ai_service_is_down()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db);
        var service = NewService(db, new ThrowingAiClient());

        var result = await service.RunAsync(data.RequestId, CancellationToken.None);

        Assert.Equal("SafeFailure", result.Status);
        Assert.True(result.RequiresHumanReview);
        Assert.Equal("AI_SERVICE_UNAVAILABLE", (await db.AgentSteps.SingleAsync(step => step.AgentName == "WeatherResourceAgent")).ErrorCode);
    }

    [Fact]
    public async Task Analysis_never_mutates_inventory_or_creates_reservations()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db);
        var reservationsBefore = await db.ResourceReservations.CountAsync();
        var transactionsBefore = await db.StockTransactions.CountAsync();

        await NewService(db, new ToolCallingAgent(db)).RunAsync(data.RequestId, CancellationToken.None);

        var stock = await db.InventoryStocks.AsNoTracking().SingleAsync();
        Assert.Equal((70m, 40m), (stock.QuantityOnHand, stock.ReservedQuantity));
        Assert.Equal(reservationsBefore, await db.ResourceReservations.CountAsync());
        Assert.Equal(transactionsBefore, await db.StockTransactions.CountAsync());
        Assert.Empty(await db.FarmTasks.ToListAsync());
        Assert.Empty(await db.IrrigationSchedules.ToListAsync());
    }

    [Fact]
    public void Validate_rejects_weather_risk_without_forecast()
    {
        var workflowId = Guid.NewGuid();
        var evidence = new WeatherResourceToolEvidence(null, [], [], true, WeatherForecastResponse.Unavailable("Nowhere", "down"));
        var output = new WeatherResourceOutput(workflowId, "Analyzed", true, [], "High", "Invented storm.", [], [], [], ResourceRequirementStatus.Unknown);

        var errors = WeatherResourceWorkflowService.Validate(output, evidence, workflowId);

        Assert.Contains(errors, error => error.Contains("without forecast data"));
    }

    [Fact]
    public void Validate_rejects_crop_health_consideration_for_unknown_action()
    {
        var workflowId = Guid.NewGuid();
        var evidence = new WeatherResourceToolEvidence(null, [], [], true, WeatherForecastResponse.Unavailable("Nowhere", "down"));
        var context = new Member2FieldAnalysisContext(
            "Suitable", "", "", "", [], "Ready", [], [], "Medium", [], false,
            [new Member3CropHealthActionContext("known-action", CropHealthActionType.MonitorSymptoms, 0, "EarlyGrowth")]);
        var input = new WeatherResourceInput(workflowId, Guid.NewGuid(), Guid.NewGuid(), null, "Kandy",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            "Medium", "", context);
        var output = new WeatherResourceOutput(workflowId, "Analyzed", true, [], "Unknown", "No forecast.", [], [], [],
            ResourceRequirementStatus.Unknown, CropHealthConsiderations:
            [new CropHealthWeatherResourceConsideration("unknown-action", "WeatherTimingConstraint", "Delay while heavy rain continues.")]);

        var errors = WeatherResourceWorkflowService.Validate(output, evidence, workflowId, input);

        Assert.Contains(errors, error => error.Contains("unknown Member 2 action", StringComparison.Ordinal));
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

    // SAMPLE explanation for the stub forecast (2026-09-15: 12 mm rain, 31 C, 5 m/s), so daily rain makes it Medium.
    private static WeatherRiskAssessment SampleAssessment() =>
        new("Medium", "Medium weather risk: 12 mm of rain is expected on 2026-09-15.",
            "The heaviest daily rain of 12 mm reaches the 10 mm Medium threshold; temperature and wind stay below theirs.",
            [
                new WeatherRiskFactor("DailyRainfall", "Heaviest daily rain", 12, "mm", new DateOnly(2026, 9, 15), 10, 30, "Medium", "12 mm on 2026-09-15."),
                new WeatherRiskFactor("TotalRainfall", "Total forecast rain", 12, "mm", null, 30, 80, "Low", "12 mm over 1 day."),
                new WeatherRiskFactor("MaxTemperature", "Highest temperature", 31, "C", new DateOnly(2026, 9, 15), 34, 38, "Low", "31 C."),
                new WeatherRiskFactor("MaxWind", "Strongest wind", 5, "m/s", new DateOnly(2026, 9, 15), 10, 15, "Low", "5 m/s.")
            ],
            ["Rain can delay land preparation and wash fertilizer away."],
            [new WeatherRiskAction("Clear drainage channels before the rain.", "Before 2026-09-15", "Medium")],
            "Check the forecast again before field work.",
            "OpenAI");

    [Fact]
    public async Task Run_stores_the_weather_risk_explanation_for_member4_and_the_officer()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db);
        var agent = new ToolCallingAgent(db, output => output with { WeatherRiskAssessment = SampleAssessment() });

        var result = await NewService(db, agent).RunAsync(data.RequestId, CancellationToken.None);

        Assert.Equal("Analyzed", result.Status);
        var stored = await NewService(db, agent).GetResultAsync(data.RequestId, CancellationToken.None);
        Assert.Equal("OpenAI", stored.WeatherRiskAssessment!.GeneratedBy);
        Assert.Equal(12m, stored.WeatherRiskAssessment.ContributingFactors.Single(factor => factor.Metric == "DailyRainfall").Value);
        Assert.Equal("Clear drainage channels before the rain.", stored.WeatherRiskAssessment.RecommendedActions.Single().Action);
    }

    [Fact]
    public async Task Run_rejects_a_weather_explanation_that_changes_the_risk_or_the_forecast_figures()
    {
        static WeatherRiskAssessment Factor(string metric, Func<WeatherRiskFactor, WeatherRiskFactor> change) =>
            SampleAssessment() with
            {
                ContributingFactors = SampleAssessment().ContributingFactors.Select(factor => factor.Metric == metric ? change(factor) : factor).ToList()
            };

        foreach (var (assessment, expectedError) in new (WeatherRiskAssessment, string)[]
                 {
                     (SampleAssessment() with { RiskLevel = "High" }, "riskLevel must equal weatherRisk"),
                     (Factor("DailyRainfall", factor => factor with { Value = 40, Level = "High" }), "DailyRainfall does not match"),
                     (Factor("MaxTemperature", factor => factor with { ObservedOn = new DateOnly(2026, 9, 16) }), "MaxTemperature does not match"),
                     (Factor("DailyRainfall", factor => factor with { Level = "Low" }), "DailyRainfall does not match"),
                     (SampleAssessment() with { ContributingFactors = SampleAssessment().ContributingFactors.Skip(1).ToList() }, "exactly once"),
                     (SampleAssessment() with { GeneratedBy = "Guess" }, "generatedBy"),
                     (SampleAssessment() with { Headline = new string('x', 241) }, "too long")
                 })
        {
            await using var db = NewDbContext();
            var data = await SeedAsync(db);
            var agent = new ToolCallingAgent(db, output => output with { WeatherRiskAssessment = assessment });

            var result = await NewService(db, agent).RunAsync(data.RequestId, CancellationToken.None);

            Assert.Equal("SafeFailure", result.Status);
            Assert.Contains(expectedError, await db.AgentValidationResults.Select(item => item.ErrorsJson).SingleAsync());
        }
    }

    [Fact]
    public void Validate_rejects_weather_risk_factors_without_forecast()
    {
        var workflowId = Guid.NewGuid();
        var evidence = new WeatherResourceToolEvidence(null, [], [], true, WeatherForecastResponse.Unavailable("Nowhere", "down"));
        var output = new WeatherResourceOutput(workflowId, "Analyzed", true, [], "Unknown", "No forecast.", [], [], [],
            ResourceRequirementStatus.Unknown, WeatherRiskAssessment: SampleAssessment() with { RiskLevel = "Unknown" });

        var errors = WeatherResourceWorkflowService.Validate(output, evidence, workflowId);

        Assert.Contains(errors, error => error.Contains("weather risk factors without forecast data", StringComparison.Ordinal));
    }

    [Fact]
    public async Task History_keeps_each_analysed_plan_with_the_ai_result_stored_for_it()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db);
        var officer = new AppUser { FullName = "Rani Resource", Email = "rani@example.test", PasswordHash = "hash", Role = ApplicationRole.ResourceOfficer, IsActive = true };
        db.Add(officer);
        await db.SaveChangesAsync();
        var agent = new ToolCallingAgent(db, output => output with { WeatherRiskAssessment = SampleAssessment() });
        Assert.Empty((await NewService(db, agent).GetHistoryAsync(new PagedQuery(), CancellationToken.None)).Items);

        await NewService(db, agent, officer.Id).RunAsync(data.RequestId, CancellationToken.None);
        var history = await NewService(db, agent).GetHistoryAsync(new PagedQuery(), CancellationToken.None);

        var entry = Assert.Single(history.Items);
        Assert.Equal((data.WorkflowId, data.RequestId), (entry.WorkflowId, entry.CropPlanRequestId));
        Assert.Equal(("Plan the next tomato season safely.", "North Farm", "Field A", "Tomato"), (entry.Objective, entry.FarmName, entry.FieldName, entry.CropName));
        Assert.Equal(("Analyzed", "Medium", ResourceRequirementStatus.Insufficient), (entry.Status, entry.WeatherRisk, entry.RequirementStatus));
        Assert.Equal("Rani Resource", entry.RunByName);
        Assert.Equal(SampleAssessment().Headline, entry.Headline);
        Assert.Equal(AgentStepStatus.Completed, entry.StepStatus);

        var detail = await NewService(db, agent).GetHistoryEntryAsync(entry.WorkflowId, CancellationToken.None);
        Assert.Equal(entry, detail.Plan);
        Assert.Equal(SampleAssessment().Explanation, detail.Result.WeatherRiskAssessment!.Explanation);
        Assert.Equal(20m, detail.Result.ResourceRequirements!.Single().ShortageQuantity);
        Assert.Equal(["Review stock before planting."], detail.Result.Recommendations);
    }

    [Fact]
    public async Task History_includes_safe_failures_and_supports_search()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db);
        await NewService(db, new ThrowingAiClient()).RunAsync(data.RequestId, CancellationToken.None);
        var service = NewService(db, new ThrowingAiClient());

        var entry = Assert.Single((await service.GetHistoryAsync(new PagedQuery { Search = "tomato" }, CancellationToken.None)).Items);
        Assert.Equal(("SafeFailure", "Unknown", AgentStepStatus.Failed), (entry.Status, entry.WeatherRisk, entry.StepStatus));
        Assert.Null(entry.Headline);
        Assert.Empty((await service.GetHistoryAsync(new PagedQuery { Search = "no such farm" }, CancellationToken.None)).Items);
        var missing = await Assert.ThrowsAsync<ApiException>(() => service.GetHistoryEntryAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Equal("NOT_FOUND", missing.Code);
    }

    private static WeatherResourceWorkflowService NewService(AppDbContext db, IWeatherResourceAIClient aiClient, Guid? userId = null)
    {
        var officer = new StubCurrentUser(ApplicationRole.ResourceOfficer, userId);
        var cropPlanning = new CropPlanningService(
            db,
            officer,
            new FarmRequestValidator(),
            new FieldRequestValidator(),
            new CropTypeRequestValidator(),
            new CropCycleRequestValidator(),
            new CropPlanRequestCreateValidator(),
            new CropPlanRequestUpdateValidator());
        return new WeatherResourceWorkflowService(db, officer, cropPlanning, aiClient, NullLogger<WeatherResourceWorkflowService>.Instance);
    }

    private sealed class StubCurrentUser(ApplicationRole role, Guid? userId = null) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId ?? Guid.NewGuid();
        public ApplicationRole? Role { get; } = role;
        public bool IsInRole(ApplicationRole roleToCheck) => Role == roleToCheck;
    }

    private sealed class StubWeatherService : IWeatherService
    {
        public Task<WeatherForecastResponse> GetForecastAsync(string location, CancellationToken cancellationToken) =>
            Task.FromResult(new WeatherForecastResponse(location, true, "stub", [new WeatherDayResponse(new DateOnly(2026, 9, 15), 23, 31, 12, 5, "moderate rain")]));
    }

    private sealed class ThrowingAiClient : IWeatherResourceAIClient
    {
        public Task<WeatherResourceOutput> RunWeatherResourceAnalysisAsync(WeatherResourceInput input, CancellationToken cancellationToken) =>
            throw new HttpRequestException("No service.");
    }

    /// <summary>
    /// Behaves like the Python agent: calls the real read-only tool services, records each call the way
    /// InternalAgentToolsController does, and returns the output those tool results justify (optionally tampered).
    /// </summary>
    private sealed class ToolCallingAgent(AppDbContext db, Func<WeatherResourceOutput, WeatherResourceOutput>? tamper = null, bool callTools = true) : IWeatherResourceAIClient
    {
        public WeatherResourceInput? Input { get; private set; }

        public async Task<WeatherResourceOutput> RunWeatherResourceAnalysisAsync(WeatherResourceInput input, CancellationToken cancellationToken)
        {
            Input = input;
            var requirements = await new CropResourceRequirementService(db, TestConfiguration()).GetRequirementsAsync(input.CropPlanRequestId, cancellationToken);
            var tools = new WeatherResourceToolService(db, new StubWeatherService());
            var ids = requirements.Requirements
                .Where(rule => rule.Status == RequirementCalculationStatus.Calculated && rule.ResourceMatch == ResourceMatchStatus.Matched)
                .Select(rule => rule.ResourceId!.Value).ToArray();
            var stocks = await tools.GetResourceAvailabilityAsync(ids, cancellationToken);
            var weather = await tools.GetWeatherForecastAsync(input.WorkflowId, cancellationToken);
            if (callTools)
            {
                await RecordAsync(input, WeatherResourceToolNames.GetCropResourceRequirements, new { cropPlanRequestId = input.CropPlanRequestId, workflowId = input.WorkflowId, agentStepId = input.AgentStepId }, requirements);
                await RecordAsync(input, WeatherResourceToolNames.GetResourceAvailability, new { resourceIds = ids, workflowId = input.WorkflowId, agentStepId = input.AgentStepId }, stocks);
                await RecordAsync(input, WeatherResourceToolNames.GetWeatherForecast, new { workflowId = input.WorkflowId, agentStepId = input.AgentStepId }, weather);
            }

            var byResource = stocks.ToDictionary(stock => stock.ResourceId);
            var assessments = requirements.Requirements.Select(rule => WeatherResourceWorkflowService.ExpectedAssessment(rule, byResource, ids)!).ToList();
            var overall = WeatherResourceWorkflowService.OverallStatus(requirements, assessments);
            if (requirements.Requirements.Count == 0)
                assessments.Add(new ResourceRequirementAssessment(null, null, requirements.CropName, null, null, null, null, null, null, ResourceRequirementStatus.Unknown, null, requirements.Reason));
            var comparable = requirements.Requirements
                .Where(rule => rule.Status == RequirementCalculationStatus.Calculated && rule.ResourceMatch == ResourceMatchStatus.Matched)
                .ToDictionary(rule => rule.ResourceId!.Value);
            var checks = stocks.Select(stock =>
            {
                var check = new ResourceCheckResponse(stock.InventoryStockId, stock.ResourceId, stock.ResourceName, stock.Unit, stock.AvailableQuantity, stock.AvailableQuantity <= stock.LowStockThreshold);
                return comparable.TryGetValue(stock.ResourceId, out var rule)
                    ? check with
                    {
                        Requested = rule.RequiredQuantity,
                        Sufficient = stock.AvailableQuantity >= rule.RequiredQuantity,
                        RequirementStatus = stock.AvailableQuantity >= rule.RequiredQuantity ? ResourceRequirementStatus.Sufficient : ResourceRequirementStatus.Insufficient
                    }
                    : check;
            }).ToList();

            var output = new WeatherResourceOutput(input.WorkflowId, "Analyzed", true, [], "Medium", "Summary from the forecast tool.", checks,
                ["Review stock before planting."], assessments, overall, requirements.Source, "Tool-based assessment.",
                [WeatherResourceToolNames.GetCropResourceRequirements, WeatherResourceToolNames.GetResourceAvailability, WeatherResourceToolNames.GetWeatherForecast]);
            return tamper?.Invoke(output) ?? output;
        }

        private async Task RecordAsync(WeatherResourceInput input, string toolName, object toolInput, object toolOutput)
        {
            db.AgentToolExecutions.Add(new AgentToolExecution
            {
                AgentStepId = input.AgentStepId,
                ToolName = toolName,
                InputJson = JsonSerializer.Serialize(toolInput, Json),
                OutputJson = JsonSerializer.Serialize(toolOutput, Json),
                Status = AgentToolExecutionStatus.Completed
            });
            await db.SaveChangesAsync();
        }
    }
}
