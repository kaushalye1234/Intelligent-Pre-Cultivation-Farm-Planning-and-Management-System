namespace AgriAssist.Api.Dtos.CropPlanning;

public sealed record AgentToolResponse<T>(
    Guid? WorkflowId,
    string ToolName,
    string Status,
    T? Data,
    string? SafeError);

public sealed record AgentCropPlanContextResponse(
    Guid Id,
    Guid FarmId,
    Guid? FieldId,
    Guid CropTypeId,
    Guid RequestedByUserId,
    DateOnly PreferredStartDate,
    DateOnly PreferredEndDate,
    decimal Budget,
    string Objective,
    string Status,
    AgentFarmDetailsResponse Farm,
    AgentFieldDetailsResponse? Field,
    AgentCropTypeDetailsResponse CropType,
    Guid? CropVarietyId = null,
    string? CropVarietyName = null,
    string CultivationSeason = "NotSure",
    Guid? PreviousCropTypeId = null,
    string? PreviousCropTypeName = null,
    IReadOnlyList<string>? PreviousKnownProblems = null);

public sealed record AgentFarmDetailsResponse(Guid Id, string Name, string Location, decimal TotalArea, Guid OwnerUserId);
public sealed record AgentFieldDetailsResponse(Guid Id, Guid FarmId, string Name, decimal Area, string SoilType, bool IsActive);
public sealed record AgentCropTypeDetailsResponse(Guid Id, string Name, string? Description, bool IsActive);
public sealed record AgentCropCycleDetailsResponse(Guid Id, Guid FieldId, Guid CropTypeId, DateOnly PlannedStartDate, DateOnly PlannedEndDate, string Status);

public sealed record AgentCropReferenceProfileResponse(
    string ReferenceDataStatus,
    CropReferenceProfileSummary? Profile,
    IReadOnlyList<CropStageReferenceSummary> Stages,
    IReadOnlyList<CropRuleReferenceSummary> Rules,
    IReadOnlyList<string> Warnings);

public sealed record CropReferenceProfileSummary(
    Guid Id,
    Guid CropTypeId,
    string? VarietyName,
    string? Region,
    string SourceName,
    string? SourceUrl,
    string SourceVersion,
    DateTime VerifiedAt,
    bool IsActive);

public sealed record CropStageReferenceSummary(
    Guid Id,
    string StageName,
    int Sequence,
    int? TypicalMinDays,
    int? TypicalMaxDays,
    string? Notes,
    string SourceName,
    string? SourceUrl);

public sealed record CropRuleReferenceSummary(
    Guid Id,
    string RuleType,
    string RuleKey,
    string StructuredValueJson,
    string SourceName,
    string? SourceUrl,
    DateTime VerifiedAt);

public sealed record AgentInspectionSummaryResponse(
    Guid Id,
    Guid FieldId,
    Guid InspectorUserId,
    DateTime ScheduledAt,
    DateTime? CompletedAt,
    string Status,
    string Summary,
    IReadOnlyList<AgentInspectionObservationSummary> Observations);

public sealed record AgentInspectionObservationSummary(Guid Id, string ObservationType, string Notes, DateTime CreatedAt);

public sealed record AgentCropIssueSummaryResponse(
    Guid Id,
    Guid FieldInspectionId,
    string Title,
    string Description,
    string Severity,
    string Status,
    DateTime CreatedAt,
    DateTime? EscalatedAt,
    IReadOnlyList<AgentFollowUpRecommendationSummary> FollowUps);

public sealed record AgentFollowUpRecommendationSummary(Guid Id, string Recommendation, DateTime? DueAt, bool IsCompleted);

public sealed record AgentInspectionImageMetadataResponse(
    Guid Id,
    Guid FieldInspectionId,
    string Url,
    string PublicId,
    string ContentType,
    long SizeBytes,
    DateTime CreatedAt);
