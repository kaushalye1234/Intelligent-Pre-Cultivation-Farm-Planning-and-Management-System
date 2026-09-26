using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.ExternalServices.Weather;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Resources;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.Resources;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.CropPlanning;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static AgriAssist.Api.Tests.WeatherResourceTestData;

namespace AgriAssist.Api.Tests;

/// <summary>GetCropResourceRequirements and the other read-only Member 3 agent tools.</summary>
public sealed class CropResourceRequirementTests
{
    [Fact]
    public async Task Calculates_required_quantity_from_verified_rate_and_field_area()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db);

        var result = await Service(db).GetRequirementsAsync(data.RequestId, CancellationToken.None);

        Assert.Equal("Available", result.Status);
        Assert.Equal("Tomato", result.CropName);
        Assert.Equal(0.5m, result.FieldArea);
        Assert.Equal("acre", result.FieldAreaUnit);
        Assert.Equal("SAMPLE source (test fixture)", result.Source!.SourceName);
        var requirement = Assert.Single(result.Requirements);
        Assert.Equal(RequirementCalculationStatus.Calculated, requirement.Status);
        Assert.Equal(ResourceMatchStatus.Matched, requirement.ResourceMatch);
        Assert.Equal(data.ResourceId, requirement.ResourceId);
        Assert.Equal(50m, requirement.RequiredQuantity);
        Assert.Equal("kg", requirement.ResourceUnit);
        Assert.Equal("100 kg/acre x 0.5 acre = 50 kg", requirement.Basis);
    }

    [Fact]
    public async Task Converts_a_per_hectare_rate_for_fields_recorded_in_acres()
    {
        await using var db = NewDbContext();
        // SAMPLE TEST DATA ONLY: 250 kg per hectare on a 2 acre field = 250 x 0.80937128448 ha = 202.343 kg.
        var data = await SeedAsync(db, fieldArea: 2m, ruleJson: """{"resourceName":"Urea","quantityPerArea":250,"resourceUnit":"kg","areaUnit":"ha"}""");

        var requirement = Assert.Single((await Service(db).GetRequirementsAsync(data.RequestId, CancellationToken.None)).Requirements);

        Assert.Equal(202.343m, requirement.RequiredQuantity);
        Assert.Equal("hectare", requirement.AreaUnit);
        Assert.Contains("(field area 2 acre)", requirement.Basis);
    }

    [Fact]
    public async Task No_verified_profile_means_unavailable_and_no_quantity()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, ruleJson: null);

        var result = await Service(db).GetRequirementsAsync(data.RequestId, CancellationToken.None);

        Assert.Equal("Unavailable", result.Status);
        Assert.Equal("No verified crop-resource requirement is available for Tomato.", result.Reason);
        Assert.Empty(result.Requirements);
        Assert.Null(result.Source);
    }

    [Fact]
    public async Task Inactive_deleted_future_and_other_region_profiles_are_ignored()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, ruleJson: null, farmLocation: "Kurunegala, North Western");
        var inactive = Profile(data.CropTypeId, null, null, DateTime.UtcNow.AddDays(-2), SampleUreaRule);
        inactive.IsActive = false;
        var deleted = Profile(data.CropTypeId, null, null, DateTime.UtcNow.AddDays(-2), SampleUreaRule);
        deleted.IsDeleted = true;
        db.AddRange(
            inactive,
            deleted,
            Profile(data.CropTypeId, null, null, DateTime.UtcNow.AddDays(2), SampleUreaRule),
            Profile(data.CropTypeId, null, "Jaffna", DateTime.UtcNow.AddDays(-2), SampleUreaRule));
        await db.SaveChangesAsync();

        Assert.Equal("Unavailable", (await Service(db).GetRequirementsAsync(data.RequestId, CancellationToken.None)).Status);

        var regional = Profile(data.CropTypeId, null, "Kurunegala", DateTime.UtcNow.AddDays(-3), SampleUreaRule);
        db.Add(regional);
        await db.SaveChangesAsync();
        Assert.Equal(regional.Id, (await Service(db).GetRequirementsAsync(data.RequestId, CancellationToken.None)).Source!.CropReferenceProfileId);
    }

    [Fact]
    public async Task Variety_specific_profile_is_preferred_over_the_generic_crop_profile()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db);
        var variety = new CropVariety { CropTypeId = data.CropTypeId, Name = "Thilina", IsActive = true };
        db.Add(variety);
        var specific = Profile(data.CropTypeId, "Thilina", null, DateTime.UtcNow.AddDays(-5),
            """{"resourceName":"Urea","quantityPerArea":80,"resourceUnit":"kg","areaUnit":"acre"}""");
        db.Add(specific);
        var request = await db.CropPlanRequests.SingleAsync();
        request.CropVarietyId = variety.Id;
        await db.SaveChangesAsync();

        var result = await Service(db).GetRequirementsAsync(data.RequestId, CancellationToken.None);

        Assert.Equal(specific.Id, result.Source!.CropReferenceProfileId);
        Assert.Equal(40m, Assert.Single(result.Requirements).RequiredQuantity);
    }

    [Theory]
    [InlineData(null, "acre", "The crop plan has no field, so the field area is unknown.")]
    [InlineData(0d, "acre", "The field area is not recorded.")]
    [InlineData(0.5, null, "The field area unit is not configured (Resources:FieldAreaUnit).")]
    public async Task Missing_field_area_or_unit_never_produces_a_quantity(double? area, string? unit, string reason)
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, fieldArea: area is null ? null : (decimal)area.Value);

        var result = await new CropResourceRequirementService(db, TestConfiguration(unit)).GetRequirementsAsync(data.RequestId, CancellationToken.None);

        Assert.Equal("Incomplete", result.Status);
        var requirement = Assert.Single(result.Requirements);
        Assert.Null(requirement.RequiredQuantity);
        Assert.Equal(RequirementCalculationStatus.Unknown, requirement.Status);
        Assert.Equal(reason, requirement.Reason);
    }

    [Fact]
    public async Task Invalid_rule_value_is_unknown_instead_of_guessed()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, ruleJson: """{"resourceName":"Urea","resourceUnit":"kg","areaUnit":"acre"}""");

        var requirement = Assert.Single((await Service(db).GetRequirementsAsync(data.RequestId, CancellationToken.None)).Requirements);

        Assert.Equal(RequirementCalculationStatus.Unknown, requirement.Status);
        Assert.Null(requirement.RequiredQuantity);
        Assert.Contains("quantityPerArea must be a positive number", requirement.Reason);
    }

    [Fact]
    public async Task Unmatched_ambiguous_and_duplicate_resources_are_reported()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, ruleJson: null);
        var category = await db.ResourceCategories.FirstAsync();
        db.AddRange(
            new Resource { Name = "MOP", Unit = "kg", ResourceCategoryId = category.Id },
            new Resource { Name = "mop", Unit = "kg", ResourceCategoryId = category.Id });
        db.Add(Profile(data.CropTypeId, null, null, DateTime.UtcNow.AddDays(-1),
            """{"resourceName":"TSP","quantityPerArea":10,"resourceUnit":"kg","areaUnit":"acre"}""",
            """{"resourceName":"MOP","quantityPerArea":10,"resourceUnit":"kg","areaUnit":"acre"}""",
            $$"""{"resourceId":"{{data.ResourceId}}","quantityPerArea":10,"resourceUnit":"kg","areaUnit":"acre"}""",
            """{"resourceName":"urea","quantityPerArea":20,"resourceUnit":"kg","areaUnit":"acre"}"""));
        await db.SaveChangesAsync();

        var requirements = (await Service(db).GetRequirementsAsync(data.RequestId, CancellationToken.None)).Requirements;

        var tsp = requirements.Single(item => item.ResourceName == "TSP");
        Assert.Equal((ResourceMatchStatus.NotInCatalogue, 5m), (tsp.ResourceMatch, tsp.RequiredQuantity!.Value));
        Assert.Equal(ResourceMatchStatus.Ambiguous, requirements.Single(item => item.ResourceName == "MOP").ResourceMatch);
        var urea = requirements.Where(item => item.ResourceId == data.ResourceId).ToList();
        Assert.Equal(2, urea.Count);
        Assert.All(urea, item => Assert.Equal((RequirementCalculationStatus.Unknown, (decimal?)null), (item.Status, item.RequiredQuantity)));
    }

    [Fact]
    public async Task Availability_is_net_of_reservations_and_always_includes_requested_resources()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, onHand: 70, reserved: 40);
        var category = await db.ResourceCategories.FirstAsync();
        for (var index = 0; index < WeatherResourceToolService.MaxRows + 5; index++)
        {
            db.Add(new InventoryStock { Resource = new Resource { Name = $"A{index:000}", Unit = "kg", ResourceCategoryId = category.Id }, QuantityOnHand = 10 });
        }
        await db.SaveChangesAsync();
        var tools = new WeatherResourceToolService(db, new StubWeather());

        var stocks = await tools.GetResourceAvailabilityAsync([data.ResourceId], CancellationToken.None);

        Assert.Equal(WeatherResourceToolService.MaxRows, stocks.Count);
        var urea = stocks.Single(stock => stock.ResourceId == data.ResourceId);
        Assert.Equal((70m, 40m, 30m), (urea.QuantityOnHand, urea.ReservedQuantity, urea.AvailableQuantity));
    }

    [Fact]
    public async Task Reservation_and_low_stock_tools_return_only_current_read_only_rows()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, onHand: 10, reserved: 6, threshold: 5);
        var stock = await db.InventoryStocks.SingleAsync();
        db.Add(new ResourceReservation { InventoryStockId = stock.Id, RequestedByUserId = data.FarmerId, Quantity = 3, Purpose = "Released", Status = ResourceReservationStatus.Released });
        await db.SaveChangesAsync();
        var tools = new WeatherResourceToolService(db, new StubWeather());

        var reservations = await tools.GetExistingReservationsAsync([data.ResourceId], CancellationToken.None);
        var lowStock = await tools.GetLowStockStatusAsync(CancellationToken.None);
        var weather = await tools.GetWeatherForecastAsync(data.WorkflowId, CancellationToken.None);

        var reservation = Assert.Single(reservations);
        Assert.Equal((6m, "Urea", "kg"), (reservation.Quantity, reservation.ResourceName, reservation.Unit));
        Assert.Equal(4m, Assert.Single(lowStock).AvailableQuantity);
        Assert.Equal("Kurunegala", weather.Location);
    }

    [Fact]
    public void Reference_profile_validator_checks_resource_requirement_rules()
    {
        var validator = new CropReferenceProfileRequestValidator();
        CropReferenceProfileRequest Request(params string[] json) => new(Guid.NewGuid(), null, null, "Source", null, "v1", DateTime.UtcNow.AddDays(-1), [],
            json.Select(item => new CropReferenceRuleRequest("ResourceRequirement", "Urea", item)).ToList());

        Assert.Empty(validator.Validate(Request(SampleUreaRule)));
        Assert.Contains(validator.Validate(Request("""{"resourceName":"Urea","quantityPerArea":-1,"resourceUnit":"kg","areaUnit":"acre"}""")),
            error => error.Contains("quantityPerArea must be a positive number"));
        Assert.Contains(validator.Validate(Request("""{"resourceName":"Urea","quantityPerArea":1,"resourceUnit":"kg","areaUnit":"perch"}""")),
            error => error.Contains("areaUnit must be acre or hectare"));
        Assert.Contains(validator.Validate(Request(SampleUreaRule, SampleUreaRule)), error => error.Contains("only one resource requirement rule"));
    }

    [Fact]
    public async Task Tool_endpoints_require_the_tool_token_and_workflow_scope_and_log_executions()
    {
        const string token = "test-tool-token-for-member-3";
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-jwt-secret-with-at-least-32-chars",
                ["Jwt:Issuer"] = "AgriAssist",
                ["Jwt:Audience"] = "AgriAssistUsers",
                ["AI:ToolToken"] = token,
                ["Weather:ApiKey"] = ""
            }));
        });
        Seeded data;
        Guid stepId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            data = await SeedAsync(db);
            stepId = await db.AgentSteps.Where(step => step.AgentName == "WeatherResourceAgent").Select(step => step.Id).SingleAsync();
        }
        using var client = factory.CreateClient();
        var path = $"/api/internal/agent-tools/crop-resource-requirements/{data.RequestId}?workflowId={data.WorkflowId}&agentStepId={stepId}";

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync($"/api/internal/agent-tools/crop-resource-requirements/{data.RequestId}?workflowId={Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/internal/agent-tools/resource-availability")).StatusCode);

        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var requirements = await client.GetFromJsonAsync<AgentToolResponse<CropResourceRequirementsResult>>(path, json);
        Assert.Equal(50m, Assert.Single(requirements!.Data!.Requirements).RequiredQuantity);
        var availability = await client.GetFromJsonAsync<AgentToolResponse<List<StockSnapshot>>>(
            $"/api/internal/agent-tools/resource-availability?resourceIds={data.ResourceId}&workflowId={data.WorkflowId}&agentStepId={stepId}", json);
        Assert.Equal(30m, Assert.Single(availability!.Data!).AvailableQuantity);
        var weather = await client.GetFromJsonAsync<AgentToolResponse<WeatherForecastResponse>>(
            $"/api/internal/agent-tools/weather-forecast?workflowId={data.WorkflowId}&agentStepId={stepId}", json);
        Assert.False(weather!.Data!.IsAvailable);

        using var verify = factory.Services.CreateScope();
        var logged = await verify.ServiceProvider.GetRequiredService<AppDbContext>().AgentToolExecutions
            .Where(item => item.AgentStepId == stepId && item.Status == AgentToolExecutionStatus.Completed)
            .Select(item => item.ToolName).ToListAsync();
        Assert.Equal(["GetCropResourceRequirements", "GetResourceAvailability", "GetWeatherForecast"], logged.Order().ToList());
    }

    private static CropResourceRequirementService Service(AppDbContext db) => new(db, TestConfiguration());

    private sealed class StubWeather : IWeatherService
    {
        public Task<WeatherForecastResponse> GetForecastAsync(string location, CancellationToken cancellationToken) =>
            Task.FromResult(WeatherForecastResponse.Unavailable(location, "stub"));
    }
}
