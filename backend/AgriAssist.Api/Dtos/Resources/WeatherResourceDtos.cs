using AgriAssist.Api.ExternalServices.Weather;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Dtos.Inspections;
using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Dtos.Resources;

/// <summary>A read-only copy of one inventory row, returned by GetResourceAvailability and GetLowStockStatus.</summary>
public sealed record StockSnapshot(
    Guid InventoryStockId,
    Guid ResourceId,
    string ResourceName,
    string Unit,
    decimal QuantityOnHand,
    decimal ReservedQuantity,
    decimal AvailableQuantity,
    decimal LowStockThreshold);

/// <summary>One active reservation, returned by GetExistingReservations.</summary>
public sealed record ReservationSnapshot(
    Guid ReservationId,
    Guid InventoryStockId,
    Guid ResourceId,
    string ResourceName,
    string Unit,
    decimal Quantity,
    string Purpose,
    DateTime CreatedAt);

public static class ResourceRequirementStatus
{
    public const string Sufficient = "Sufficient";
    public const string Insufficient = "Insufficient";
    public const string Unknown = "ResourceRequirementUnknown";
    /// <summary>The requirement is known but cannot be compared with inventory (unit mismatch or ambiguous resource).</summary>
    public const string NotComparable = "InventoryNotComparable";
    /// <summary>Overall status only: some requirements were assessed and others could not be.</summary>
    public const string Incomplete = "Incomplete";
}

public static class WeatherResourceToolNames
{
    public const string GetCropResourceRequirements = "GetCropResourceRequirements";
    public const string GetFieldDetails = "GetFieldDetails";
    public const string GetResourceAvailability = "GetResourceAvailability";
    public const string GetExistingReservations = "GetExistingReservations";
    public const string GetLowStockStatus = "GetLowStockStatus";
    public const string GetWeatherForecast = "GetWeatherForecast";
}

/// <summary>Where the verified requirement values came from (a verified crop reference profile).</summary>
public sealed record RequirementSourceSummary(
    Guid CropReferenceProfileId,
    string SourceName,
    string? SourceUrl,
    string SourceVersion,
    DateTime VerifiedAt,
    string? Region,
    string? VarietyName);

public static class RequirementCalculationStatus
{
    public const string Calculated = "Calculated";
    public const string Unknown = "Unknown";
}

public static class ResourceMatchStatus
{
    public const string Matched = "Matched";
    public const string NotInCatalogue = "NotInCatalogue";
    public const string Ambiguous = "Ambiguous";
    public const string Unresolved = "Unresolved";
}

/// <summary>
/// One verified requirement rule and, when field area and units allow it, the deterministic
/// required quantity (verified quantity per area x field area). RequiredQuantity is null when unknown.
/// </summary>
public sealed record CalculatedResourceRequirement(
    Guid RuleId,
    string RuleKey,
    Guid? ResourceId,
    string ResourceName,
    string ResourceMatch,
    decimal? QuantityPerArea,
    string? ResourceUnit,
    string? AreaUnit,
    decimal? RequiredQuantity,
    string Status,
    string? Basis,
    string? Reason);

/// <summary>GetCropResourceRequirements tool result. Status is Available, Incomplete or Unavailable.</summary>
public sealed record CropResourceRequirementsResult(
    Guid CropPlanRequestId,
    Guid CropTypeId,
    string CropName,
    string? VarietyName,
    Guid? FieldId,
    decimal? FieldArea,
    string? FieldAreaUnit,
    string Status,
    string? Reason,
    RequirementSourceSummary? Source,
    IReadOnlyList<CalculatedResourceRequirement> Requirements);

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
    bool RequiresHumanReview,
    IReadOnlyList<Member3CropHealthActionContext>? ReviewedCropIssueActions = null);

public sealed record CropHealthWeatherResourceConsideration(
    string ActionKey,
    string ConsiderationType,
    string Note,
    int ContractVersion = Member2CropHealthContractVersions.Member3Consideration);

/// <summary>
/// Sent to the AI service. The agent gathers requirements, field, inventory, reservations and weather
/// itself through the read-only internal agent tools, passing WorkflowId and AgentStepId on every call.
/// </summary>
public sealed record WeatherResourceInput(
    Guid WorkflowId,
    Guid AgentStepId,
    Guid CropPlanRequestId,
    Guid? FieldId,
    string Location,
    DateOnly PreferredStartDate,
    DateOnly PreferredEndDate,
    string FieldPriority,
    string FieldAnalysisSummary,
    Member2FieldAnalysisContext? Member2FieldAnalysisContext = null);

/// <summary>
/// Requested and Sufficient are null (and RequirementStatus is ResourceRequirementUnknown) when no verified
/// requirement exists for the stock's resource. They are never guessed.
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

/// <summary>
/// The agent's assessment of one verified requirement against inventory. RuleId is null only for the
/// summary entry that reports no verified requirement at all.
/// </summary>
public sealed record ResourceRequirementAssessment(
    Guid? RuleId,
    Guid? ResourceId,
    string ResourceName,
    string? Unit,
    decimal? RequiredQuantity,
    decimal? AvailableQuantity,
    decimal? ReservedQuantity,
    decimal? ShortageQuantity,
    bool? Sufficient,
    string RequirementStatus,
    string? Basis,
    string? Reason);

/// <summary>
/// One forecast measure (DailyRainfall, TotalRainfall, MaxTemperature or MaxWind) compared with the fixed weather-risk
/// thresholds. Value comes from GetWeatherForecast; ObservedOn is the peak day (null for TotalRainfall).
/// </summary>
public sealed record WeatherRiskFactor(
    string Metric,
    string Label,
    decimal Value,
    string Unit,
    DateOnly? ObservedOn,
    decimal MediumThreshold,
    decimal HighThreshold,
    string Level,
    string Detail);

public sealed record WeatherRiskAction(string Action, string Timing, string Priority);

/// <summary>
/// Explains the rule-based WeatherRisk: why it has that level, the calculated factors behind it, the likely impact
/// and what the farmer should do. GeneratedBy is OpenAI (narrative written by the LLM from the factors) or RuleBased.
/// </summary>
public sealed record WeatherRiskAssessment(
    string RiskLevel,
    string Headline,
    string Explanation,
    IReadOnlyList<WeatherRiskFactor> ContributingFactors,
    IReadOnlyList<string> PotentialImpacts,
    IReadOnlyList<WeatherRiskAction> RecommendedActions,
    string MonitoringAdvice,
    string GeneratedBy);

/// <summary>Member 3 output, stored in the WeatherResourceAnalysis AgentStep and read by Member 4.</summary>
public sealed record WeatherResourceOutput(
    Guid WorkflowId,
    string Status,
    bool RequiresHumanReview,
    IReadOnlyList<string> Warnings,
    string WeatherRisk,
    string WeatherSummary,
    IReadOnlyList<ResourceCheckResponse> ResourceChecks,
    IReadOnlyList<string> Recommendations,
    IReadOnlyList<ResourceRequirementAssessment>? ResourceRequirements = null,
    string RequirementStatus = ResourceRequirementStatus.Unknown,
    RequirementSourceSummary? RequirementSource = null,
    string? Reason = null,
    IReadOnlyList<string>? ToolsUsed = null,
    IReadOnlyList<CropHealthWeatherResourceConsideration>? CropHealthConsiderations = null,
    WeatherRiskAssessment? WeatherRiskAssessment = null);

public sealed record WeatherResourceRunResponse(
    Guid WorkflowId,
    Guid CropPlanRequestId,
    Guid WeatherResourceStepId,
    string Status,
    string WeatherRisk,
    bool RequiresHumanReview,
    IReadOnlyList<string> Warnings,
    string RequirementStatus = ResourceRequirementStatus.Unknown);

/// <summary>
/// One Resource Officer work-queue row: a crop plan whose latest workflow is waiting at WeatherResourceAgent.
/// Safe plan metadata only; the Member 2 context is read separately through the Member 3 handoff.
/// ReadyAt is when Member 2 field analysis completed (a workflow timestamp for legacy data).
/// </summary>
public sealed record WeatherResourceWorkItemResponse(
    Guid WorkflowId,
    Guid CropPlanRequestId,
    Guid WeatherResourceStepId,
    string Objective,
    string FarmName,
    string FarmLocation,
    Guid? FieldId,
    string? FieldName,
    string CropName,
    string? CropVarietyName,
    DateOnly PreferredStartDate,
    DateOnly PreferredEndDate,
    int CandidateRevision,
    int WorkflowVersion,
    AgentStepStatus StepStatus,
    DateTime ReadyAt,
    DateTime? StartedAt,
    string? ErrorCode,
    string? ErrorMessageSafe);

/// <summary>What the backend itself returned to the agent's tool calls for one step; used to validate the output.</summary>
public sealed record WeatherResourceToolEvidence(
    CropResourceRequirementsResult? Requirements,
    IReadOnlyList<StockSnapshot> Stocks,
    IReadOnlyCollection<Guid> AvailabilityRequestedResourceIds,
    bool AvailabilityRetrieved,
    WeatherForecastResponse? Weather);
