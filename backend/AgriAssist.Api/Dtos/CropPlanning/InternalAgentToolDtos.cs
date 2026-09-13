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
    AgentCropTypeDetailsResponse CropType);

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
