using System.Text.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Resources;
using AgriAssist.Api.Models.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AgriAssist.Api.Tests;

/// <summary>
/// Shared Member 3 test data. The Tomato/Urea "100 kg per acre" rule is SAMPLE TEST DATA ONLY, taken from the
/// worked example so the arithmetic can be checked; it is not an agronomic recommendation.
/// </summary>
internal static class WeatherResourceTestData
{
    public const string SampleUreaRule = """{"resourceName":"Urea","quantityPerArea":100,"resourceUnit":"kg","areaUnit":"acre"}""";

    public sealed record Seeded(Guid RequestId, Guid WorkflowId, Guid FieldId, Guid StockId, Guid ResourceId, Guid CropTypeId, Guid FarmerId);

    public static AppDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public static IConfiguration TestConfiguration(string? fieldAreaUnit = "acre") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Resources:FieldAreaUnit"] = fieldAreaUnit })
            .Build();

    public static async Task<Seeded> SeedAsync(
        AppDbContext db,
        bool fieldAnalysisDone = true,
        decimal? fieldArea = 0.5m,
        decimal onHand = 70,
        decimal reserved = 40,
        decimal threshold = 5,
        string resourceName = "Urea",
        string resourceUnit = "kg",
        string? ruleJson = SampleUreaRule,
        string farmLocation = "Kurunegala",
        string? farmDistrict = null,
        string cropTypeName = "Tomato")
    {
        var farmer = new AppUser { FullName = "Farmer", Email = $"farmer.{Guid.NewGuid():N}@example.test", PasswordHash = "hash", Role = ApplicationRole.Farmer, IsActive = true };
        var farm = new Farm { Name = "North Farm", Location = farmLocation, District = farmDistrict, TotalArea = 10, OwnerUser = farmer };
        var field = fieldArea is null ? null : new Field { Name = "Field A", Area = fieldArea.Value, SoilType = "Loam", Farm = farm, IsActive = true };
        var cropType = new CropType { Name = cropTypeName, IsActive = true };
        var request = new CropPlanRequest
        {
            Farm = farm,
            Field = field,
            CropType = cropType,
            RequestedByUser = farmer,
            PreferredStartDate = new DateOnly(2026, 10, 1),
            PreferredEndDate = new DateOnly(2027, 1, 1),
            Budget = 12000,
            Objective = "Plan the next tomato season safely.",
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
        var resource = new Resource { Name = resourceName, Unit = resourceUnit, ResourceCategory = new ResourceCategory { Name = cropTypeName == "Tomato" ? "Fertilizer" : "Fertilizer " + cropTypeName } };
        var stock = new InventoryStock { Resource = resource, QuantityOnHand = onHand, ReservedQuantity = reserved, LowStockThreshold = threshold };
        db.AddRange(farmer, farm, cropType, request, workflow, stock);
        if (field is not null) db.Add(field);
        if (reserved > 0)
        {
            db.Add(new ResourceReservation { InventoryStock = stock, RequestedByUser = farmer, Quantity = reserved, Purpose = "Existing plan", Status = ResourceReservationStatus.Active });
        }
        if (ruleJson is not null) db.Add(Profile(cropType, ruleJson));
        await db.SaveChangesAsync();
        return new Seeded(request.Id, workflow.Id, field?.Id ?? Guid.Empty, stock.Id, resource.Id, cropType.Id, farmer.Id);
    }

    public static CropReferenceProfile Profile(CropType cropType, params string[] ruleJson) => Profile(cropType.Id, null, null, DateTime.UtcNow.AddDays(-1), ruleJson);

    public static CropReferenceProfile Profile(Guid cropTypeId, string? varietyName, string? region, DateTime verifiedAt, params string[] ruleJson) =>
        new()
        {
            CropTypeId = cropTypeId,
            VarietyName = varietyName,
            Region = region,
            SourceName = "SAMPLE source (test fixture)",
            SourceVersion = "test",
            VerifiedAt = verifiedAt,
            Rules = ruleJson.Select((json, index) => new CropRuleReference
            {
                RuleType = "ResourceRequirement",
                RuleKey = $"Rule {index + 1}",
                StructuredValueJson = json,
                SourceName = "SAMPLE source (test fixture)",
                VerifiedAt = verifiedAt
            }).ToList()
        };
}
