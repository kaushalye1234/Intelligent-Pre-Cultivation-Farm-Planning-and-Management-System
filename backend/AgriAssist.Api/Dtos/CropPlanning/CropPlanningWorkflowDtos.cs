using AgriAssist.Api.Models.Inspections;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Dtos.Inspections;
using System.Text.Json.Serialization;

namespace AgriAssist.Api.Dtos.CropPlanning;

public sealed record CropPlanningWorkflowStartResponse(
    Guid WorkflowId,
    Guid CropPlanRequestId,
    Guid CoordinatorStepId,
    string Status,
    bool RequiresHumanReview,
    IReadOnlyList<string> Warnings);

public sealed record CropPlanningWorkflowStatusResponse(
    Guid WorkflowId,
    Guid CropPlanRequestId,
    AgentWorkflowStatus Status,
    string CurrentStep,
    string StatusCode,
    string StatusLabel,
    string OverallStatusCode,
    string OverallStatusLabel,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    IReadOnlyList<AgentStepStatusResponse> Steps,
    IReadOnlyList<string> Warnings);

public sealed record AgentStepStatusResponse(
    Guid Id,
    string AgentName,
    string StepName,
    int Sequence,
    AgentStepStatus Status,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? ErrorCode,
    string? ErrorMessageSafe);

public sealed record CropPlanningResultResponse(
    Guid WorkflowId,
    string Status,
    bool RequiresHumanReview,
    IReadOnlyList<string> Warnings,
    string ReferenceDataStatus,
    string ObjectiveSummary,
    IReadOnlyList<CropPlanningDelegatedStepResponse> Steps);

public sealed record CropPlanningDelegatedStepResponse(int Sequence, string StepType, string AssignedAgent);

public sealed record CropPlanningCoordinatorInput(
    Guid WorkflowId,
    Guid CropPlanRequestId,
    Guid FarmerId,
    Guid FarmId,
    Guid? FieldId,
    Guid? CropCycleId,
    Guid CropTypeId,
    string Objective,
    decimal Budget,
    DateOnly PreferredStartDate,
    Guid? CropVarietyId = null,
    string? CropVarietyName = null,
    string CultivationSeason = "NotSure",
    DateOnly PreferredEndDate = default,
    Guid? PreviousCropTypeId = null,
    string? PreviousCropTypeName = null,
    IReadOnlyList<string>? PreviousKnownProblems = null);

public sealed record CropPlanningCoordinatorOutput(
    Guid WorkflowId,
    string Status,
    bool RequiresHumanReview,
    IReadOnlyList<string> Warnings,
    string? ReferenceDataStatus,
    string? ObjectiveSummary,
    IReadOnlyList<CropPlanningDelegatedStepResponse>? Steps);

public sealed record FieldAnalysisInput(
    Guid WorkflowId,
    Guid CropPlanRequestId,
    Guid PrePlantingInspectionId,
    Guid FieldId,
    Guid? CropCycleId,
    IReadOnlyList<string> RequestedAnalysis,
    Guid? CropReferenceProfileId,
    Guid? AgentStepId,
    ReviewedImageAnalysisProjection? ReviewedImageAnalysis = null);

[JsonConverter(typeof(JsonStringEnumConverter<PrePlantingSoilType>))]
public enum PrePlantingSoilType { Sandy, Clay, Loamy, Silty, Mixed, Unknown, Other }

[JsonConverter(typeof(JsonStringEnumConverter<PrePlantingSoilCondition>))]
public enum PrePlantingSoilCondition { Good, Moderate, Poor, Compacted, Eroded, Unknown, Other }

[JsonConverter(typeof(JsonStringEnumConverter<PrePlantingSoilMoisture>))]
public enum PrePlantingSoilMoisture { Dry, Moist, Wet, Waterlogged, Unknown }

[JsonConverter(typeof(JsonStringEnumConverter<PrePlantingWaterAvailability>))]
public enum PrePlantingWaterAvailability { Adequate, Limited, Unavailable, Seasonal, Unknown }

[JsonConverter(typeof(JsonStringEnumConverter<PrePlantingIrrigationAvailability>))]
public enum PrePlantingIrrigationAvailability { Available, Limited, Unavailable, NotRequired, Unknown }

[JsonConverter(typeof(JsonStringEnumConverter<PrePlantingWaterReliability>))]
public enum PrePlantingWaterReliability { Reliable, Intermittent, Seasonal, Unreliable, Unknown }

[JsonConverter(typeof(JsonStringEnumConverter<PrePlantingDrainageCondition>))]
public enum PrePlantingDrainageCondition { Good, Moderate, Poor, Unknown }

[JsonConverter(typeof(JsonStringEnumConverter<PrePlantingWaterloggingRisk>))]
public enum PrePlantingWaterloggingRisk { NoneObserved, Low, Moderate, High, Unknown }

[JsonConverter(typeof(JsonStringEnumConverter<PrePlantingGeneralFieldCondition>))]
public enum PrePlantingGeneralFieldCondition
{
    ClearAndPrepared,
    RequiresLandPreparation,
    UnevenField,
    Waterlogged,
    TooDry,
    ErosionPresent,
    AccessLimitation,
    Other
}

[JsonConverter(typeof(JsonStringEnumConverter<PrePlantingPlantingReadiness>))]
public enum PrePlantingPlantingReadiness
{
    Ready,
    ReadyWithMinorPreparation,
    RequiresPreparation,
    NotReady,
    RequiresFurtherAssessment
}

[JsonConverter(typeof(JsonStringEnumConverter<PrePlantingRisk>))]
public enum PrePlantingRisk
{
    WaterShortageRisk,
    FloodingRisk,
    PoorDrainage,
    SoilSuitabilityConcern,
    SoilErosion,
    FieldAccessProblem,
    LandPreparationRequired,
    Other
}

public sealed record PrePlantingAssessmentRequest
{
    public PrePlantingSoilType? SoilType { get; init; }
    public PrePlantingSoilCondition? SoilCondition { get; init; }
    public PrePlantingSoilMoisture? SoilMoisture { get; init; }
    public string? SoilNotes { get; init; }
    public PrePlantingWaterAvailability? WaterAvailability { get; init; }
    public string? MainWaterSource { get; init; }
    public PrePlantingIrrigationAvailability? IrrigationAvailability { get; init; }
    public PrePlantingWaterReliability? WaterReliability { get; init; }
    public string? WaterConcerns { get; init; }
    public PrePlantingDrainageCondition? DrainageCondition { get; init; }
    public PrePlantingWaterloggingRisk? WaterloggingRisk { get; init; }
    public string? DrainageNotes { get; init; }
    public PrePlantingGeneralFieldCondition? GeneralFieldCondition { get; init; }
    public string? GeneralFieldNotes { get; init; }
    public PrePlantingPlantingReadiness? PlantingReadiness { get; init; }
    public IReadOnlyList<PrePlantingRisk>? IdentifiedRisks { get; init; }
    public string? RiskNotes { get; init; }
    public string? RisksAndConcerns { get; init; }
    public string? OfficerNotes { get; init; }
}

public sealed record PrePlantingAssessmentImageResponse(
    Guid Id,
    string Url,
    string ContentType,
    long SizeBytes,
    bool IsRepresentativeForAi);

public sealed record PrePlantingAssessmentResponse
{
    public Guid InspectionId { get; init; }
    public Guid CropPlanRequestId { get; init; }
    public Guid FieldId { get; init; }
    public Guid InspectorUserId { get; init; }
    public InspectionStatus Status { get; init; }
    public DateTime ScheduledAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public PrePlantingSoilType? SoilType { get; init; }
    public PrePlantingSoilCondition? SoilCondition { get; init; }
    public PrePlantingSoilMoisture? SoilMoisture { get; init; }
    public string? SoilNotes { get; init; }
    public PrePlantingWaterAvailability? WaterAvailability { get; init; }
    public string? MainWaterSource { get; init; }
    public PrePlantingIrrigationAvailability? IrrigationAvailability { get; init; }
    public PrePlantingWaterReliability? WaterReliability { get; init; }
    public string? WaterConcerns { get; init; }
    public PrePlantingDrainageCondition? DrainageCondition { get; init; }
    public PrePlantingWaterloggingRisk? WaterloggingRisk { get; init; }
    public string? DrainageNotes { get; init; }
    public PrePlantingGeneralFieldCondition? GeneralFieldCondition { get; init; }
    public string? GeneralFieldNotes { get; init; }
    public PrePlantingPlantingReadiness? PlantingReadiness { get; init; }
    public IReadOnlyList<PrePlantingRisk>? IdentifiedRisks { get; init; }
    public string? RiskNotes { get; init; }
    public string? RisksAndConcerns { get; init; }
    public string? OfficerNotes { get; init; }
    public IReadOnlyList<PrePlantingAssessmentImageResponse> Images { get; init; } = [];
}

public sealed record PrePlantingContextResponse(
    Guid CropPlanRequestId,
    Guid WorkflowId,
    string CurrentStep,
    Guid FarmerId,
    string FarmerName,
    string? FarmerPhoneNumber,
    string? FarmerContactAddress,
    Guid FarmId,
    string FarmName,
    string FarmLocation,
    string? FarmDistrict,
    Guid FieldId,
    string FieldName,
    Guid CropTypeId,
    string CropName,
    Guid? CropVarietyId,
    string? CropVarietyName,
    CultivationSeason CultivationSeason,
    DateOnly PreferredStartDate,
    DateOnly PreferredEndDate);

public sealed record FieldAnalysisFieldConditionResponse(
    string Summary,
    IReadOnlyList<Guid> EvidenceInspectionIds);

public sealed record FieldAnalysisOpenIssueResponse(
    Guid IssueId,
    string Severity,
    string Status,
    Guid? EvidenceInspectionId);

public sealed record FieldAnalysisOutput(
    Guid WorkflowId,
    string Status,
    bool RequiresHumanReview,
    IReadOnlyList<string> Warnings,
    FieldAnalysisFieldConditionResponse FieldCondition,
    IReadOnlyList<FieldAnalysisOpenIssueResponse> OpenIssues,
    string Priority,
    string FieldSuitability = "Unknown",
    string SoilAssessment = "",
    string WaterAssessment = "",
    string DrainageAssessment = "",
    IReadOnlyList<string>? FieldPreparationRequirements = null,
    string PlantingReadiness = "Unknown",
    IReadOnlyList<PrePlantingRisk>? IdentifiedRisks = null,
    IReadOnlyList<string>? RecommendedPrePlantingActions = null,
    IReadOnlyList<ReviewedCropIssueActionResponse>? ReviewedCropIssueActions = null,
    ReviewedCropHealthGuidanceSource? ReviewedCropHealthGuidance = null);

public sealed record ReviewedCropIssueActionResponse(
    CropHealthActionType ActionType,
    int Order,
    string Title,
    string Description,
    string TimingCategory,
    string ResponsibleRole,
    string Origin,
    Guid InspectionId,
    Guid InspectionImageId,
    Guid AnalysisId,
    Guid ReviewId,
    IReadOnlyList<ImageAnalysisSourceReference> SourceReferences);

public sealed record ReviewedCropHealthGuidanceSource(
    IReadOnlyList<string> VisibleFindings,
    IReadOnlyList<string> PossibleConcerns,
    string Severity,
    string Uncertainty,
    bool RequiresFurtherAssessment,
    Guid InspectionId,
    Guid InspectionImageId,
    Guid AnalysisId,
    Guid ReviewId);

public sealed record FieldAnalysisRunResponse(
    Guid WorkflowId,
    Guid CropPlanRequestId,
    Guid FieldAnalysisStepId,
    string Status,
    bool RequiresHumanReview,
    IReadOnlyList<string> Warnings);

public sealed record Member3HandoffResponse(
    Guid WorkflowId,
    Guid CropPlanRequestId,
    Guid? FieldId,
    Guid? CropCycleId,
    string FieldLocationContext,
    DateOnly PreferredStartDate,
    DateOnly PreferredEndDate,
    string FieldAnalysisSummary,
    string Priority,
    IReadOnlyList<string> Warnings,
    bool RequiresHumanReview,
    string FieldSuitability,
    string SoilAssessment,
    string WaterAssessment,
    string DrainageAssessment,
    IReadOnlyList<string> FieldPreparationRequirements,
    string PlantingReadiness,
    IReadOnlyList<PrePlantingRisk> IdentifiedRisks,
    IReadOnlyList<string> RecommendedPrePlantingActions,
    IReadOnlyList<Member3CropHealthActionContext>? ReviewedCropIssueActions = null);

public sealed record Member3CropHealthActionContext(
    string ActionKey,
    CropHealthActionType ActionType,
    int Order,
    string TimingCategory);

public sealed record FarmerApprovedTaskResponse(
    Guid Id,
    string Title,
    string Description,
    DateTime DueAt,
    string Status);

public sealed record FarmerApprovedIrrigationResponse(
    Guid Id,
    DateTime ScheduledAt,
    int DurationMinutes,
    string Notes,
    string Status);

public sealed record FarmerApprovedCropHealthResponse(
    string CropHealthObservation,
    string PossibleConcern,
    string UncertaintyGuidance,
    IReadOnlyList<string> ApprovedPrePlantingActions,
    IReadOnlyList<string> ApprovedMonitoringActions,
    string? EscalationGuidance,
    string WhyThisIsRecommended);

public sealed record FarmerApprovedPlanResponse(
    int ContractVersion,
    Guid CropPlanRequestId,
    string Objective,
    string CropName,
    string? VarietyName,
    DateOnly PreferredStartDate,
    DateOnly PreferredEndDate,
    DateTime ApprovedAt,
    string? FieldSummary,
    string? WeatherSummary,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Recommendations,
    IReadOnlyList<FarmerApprovedTaskResponse> ApprovedTasks,
    IReadOnlyList<FarmerApprovedIrrigationResponse> ApprovedIrrigationSchedules,
    FarmerApprovedCropHealthResponse? CropHealth);
