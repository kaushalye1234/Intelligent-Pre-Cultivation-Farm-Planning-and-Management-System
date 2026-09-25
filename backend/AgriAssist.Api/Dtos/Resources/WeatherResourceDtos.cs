using AgriAssist.Api.ExternalServices.Weather;
using AgriAssist.Api.Dtos.CropPlanning;

namespace AgriAssist.Api.Dtos.Resources;

/// <summary>A read-only copy of one inventory row, taken when the Member 3 step runs.</summary>
public sealed record StockSnapshot(
    Guid InventoryStockId,
    Guid ResourceId,
    string ResourceName,
    string Unit,
    decimal QuantityOnHand,
    decimal ReservedQuantity,
    decimal AvailableQuantity,
    decimal LowStockThreshold);

/// <summary>
/// A quantity of one resource that crop planning says the plan needs. Crop planning does not produce
/// these yet, so the list is normally empty and every resource check reports ResourceRequirementUnknown.
/// </summary>
public sealed record ResourceRequirement(Guid ResourceId, decimal RequestedQuantity);

public static class ResourceRequirementStatus
{
    public const string Sufficient = "Sufficient";
    public const string Insufficient = "Insufficient";
    public const string Unknown = "ResourceRequirementUnknown";
}

/// <summary>
/// Safe, read-only Member 2 analysis context. It deliberately excludes raw observations,
/// staff notes, images, evidence identifiers, and crop-issue identifiers.
/// </summary>
public sealed record Member2FieldAnalysisContext(
    string FieldSuitability,
    string SoilAssessment,
    string WaterAssessment,
    string DrainageAssessment,
    IReadOnlyList<string> FieldPreparationRequirements,
    string PlantingReadiness,
    IReadOnlyList<PrePlantingRisk> IdentifiedRisks,
    IReadOnlyList<string> RecommendedPrePlantingActions,
    string Priority,
    IReadOnlyList<string> Warnings,
    bool RequiresHumanReview);

/// <summary>Sent to the AI service. All evidence is gathered by ASP.NET so the agent needs no tool calls.</summary>
public sealed record WeatherResourceInput(
    Guid WorkflowId,
    Guid AgentStepId,
    Guid CropPlanRequestId,
    string Location,
    DateOnly PreferredStartDate,
    DateOnly PreferredEndDate,
    string FieldPriority,
    string FieldAnalysisSummary,
    WeatherForecastResponse Weather,
    IReadOnlyList<StockSnapshot> Stocks,
    IReadOnlyList<ResourceRequirement>? ResourceRequirements = null,
    Member2FieldAnalysisContext? Member2FieldAnalysisContext = null);

/// <summary>
/// Requested and Sufficient are null (and RequirementStatus is ResourceRequirementUnknown) when crop
/// planning did not state how much of the resource is needed. They are never guessed.
/// </summary>
public sealed record ResourceCheckResponse(
    Guid InventoryStockId,
    Guid ResourceId,
    string ResourceName,
    string Unit,
    decimal AvailableQuantity,
    bool IsLowStock,
    decimal? Requested = null,
    bool? Sufficient = null,
    string RequirementStatus = ResourceRequirementStatus.Unknown);

/// <summary>Member 3 output, stored in the WeatherResourceAnalysis AgentStep and read by Member 4.</summary>
public sealed record WeatherResourceOutput(
    Guid WorkflowId,
    string Status,
    bool RequiresHumanReview,
    IReadOnlyList<string> Warnings,
    string WeatherRisk,
    string WeatherSummary,
    IReadOnlyList<ResourceCheckResponse> ResourceChecks,
    IReadOnlyList<string> Recommendations);

public sealed record WeatherResourceRunResponse(
    Guid WorkflowId,
    Guid CropPlanRequestId,
    Guid WeatherResourceStepId,
    string Status,
    string WeatherRisk,
    bool RequiresHumanReview,
    IReadOnlyList<string> Warnings);
