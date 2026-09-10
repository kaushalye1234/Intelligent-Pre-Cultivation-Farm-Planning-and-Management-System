using AgriAssist.Api.Models.Inspections;

namespace AgriAssist.Api.Dtos.Inspections;

public sealed record FieldInspectionRequest(Guid FieldId, DateTime ScheduledAt, InspectionStatus Status, string Summary);
public sealed record FieldInspectionResponse(Guid Id, Guid FieldId, Guid InspectorUserId, DateTime ScheduledAt, DateTime? CompletedAt, InspectionStatus Status, string Summary);

public sealed record ObservationRequest(Guid FieldInspectionId, string ObservationType, string Notes);
public sealed record ObservationResponse(Guid Id, Guid FieldInspectionId, string ObservationType, string Notes);

public sealed record CropIssueRequest(Guid FieldInspectionId, string Title, string Description, CropIssueSeverity Severity, CropIssueStatus Status);
public sealed record CropIssueResponse(Guid Id, Guid FieldInspectionId, string Title, string Description, CropIssueSeverity Severity, CropIssueStatus Status, DateTime? EscalatedAt);

public sealed record FollowUpRecommendationRequest(Guid CropIssueId, string Recommendation, DateTime? DueAt, bool IsCompleted);
public sealed record FollowUpRecommendationResponse(Guid Id, Guid CropIssueId, string Recommendation, DateTime? DueAt, bool IsCompleted);

public sealed record InspectionImageResponse(Guid Id, Guid FieldInspectionId, string Url, string PublicId, string ContentType, long SizeBytes);
