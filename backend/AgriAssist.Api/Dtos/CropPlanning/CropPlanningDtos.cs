using AgriAssist.Api.Models.CropPlanning;

namespace AgriAssist.Api.Dtos.CropPlanning;

public sealed record FarmRequest(string Name, string Location, decimal TotalArea, Guid? OwnerUserId);
public sealed record FarmResponse(Guid Id, string Name, string Location, decimal TotalArea, Guid OwnerUserId, DateTime CreatedAt);
public sealed record FarmerOnboardingStatusResponse(string Stage, int ActiveFarmCount, int ActiveFieldCount);

public sealed record FieldRequest(Guid FarmId, string Name, decimal Area, string SoilType, bool IsActive);
public sealed record FieldResponse(Guid Id, Guid FarmId, string Name, decimal Area, string SoilType, bool IsActive);

public sealed record CropTypeRequest(string Name, string? Description, bool IsActive);
public sealed record CropTypeResponse(Guid Id, string Name, string? Description, bool IsActive);
public sealed record CropVarietyRequest(Guid CropTypeId, string Name, bool IsActive);
public sealed record CropVarietyResponse(Guid Id, Guid CropTypeId, string Name, bool IsActive);

public sealed record CropReferenceStageRequest(string StageName, int Sequence, int? TypicalMinDays, int? TypicalMaxDays, string? Notes);
public sealed record CropReferenceRuleRequest(string RuleType, string RuleKey, string StructuredValueJson);
public sealed record CropReferenceProfileRequest(
    Guid CropTypeId, Guid? CropVarietyId, string? Region, string SourceName, string? SourceUrl,
    string SourceVersion, DateTime VerifiedAt, IReadOnlyList<CropReferenceStageRequest> Stages,
    IReadOnlyList<CropReferenceRuleRequest> Rules);
public sealed record CropReferenceProfileResponse(
    Guid Id, Guid CropTypeId, string? VarietyName, string? Region, string SourceName, string? SourceUrl,
    string SourceVersion, DateTime VerifiedAt, bool IsActive, int StageCount, int RuleCount);

public sealed record CropCycleRequest(Guid FieldId, Guid CropTypeId, DateOnly PlannedStartDate, DateOnly PlannedEndDate, CropCycleStatus Status);
public sealed record CropCycleResponse(Guid Id, Guid FieldId, Guid CropTypeId, DateOnly PlannedStartDate, DateOnly PlannedEndDate, CropCycleStatus Status);

public sealed record CropPlanRequestCreate(Guid FarmId, Guid? FieldId, Guid CropTypeId, DateOnly PreferredStartDate, DateOnly PreferredEndDate, decimal Budget, string Objective);
public sealed record CropPlanRequestUpdate(DateOnly PreferredStartDate, DateOnly PreferredEndDate, decimal Budget, string Objective, CropPlanRequestStatus Status);
public sealed record CropPlanRequestResponse(Guid Id, Guid FarmId, Guid? FieldId, Guid CropTypeId, Guid RequestedByUserId, DateOnly PreferredStartDate, DateOnly PreferredEndDate, decimal Budget, string Objective, CropPlanRequestStatus Status, DateTime CreatedAt);
public sealed record CropPlanHistoryResponse(Guid Id, Guid CropPlanRequestId, CropPlanRequestStatus FromStatus, CropPlanRequestStatus ToStatus, string Note, Guid ChangedByUserId, DateTime CreatedAt);
