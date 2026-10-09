using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Models.Inspections;

/// <summary>
/// Append-only Field Officer disposition for one immutable image-analysis result.
/// </summary>
public sealed class InspectionImageAnalysisReview : AuditableEntity
{
    public Guid InspectionImageAnalysisId { get; set; }
    public InspectionImageAnalysis? InspectionImageAnalysis { get; set; }
    public Guid ReviewedByUserId { get; set; }
    public AppUser? ReviewedByUser { get; set; }
    public ImageAnalysisReviewDisposition Disposition { get; set; }
    public string? ReviewedProjectionJson { get; set; }
    public string EditedFieldsJson { get; set; } = "[]";
    public string? StaffNote { get; set; }
    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;
}

public enum ImageAnalysisReviewDisposition
{
    Accepted = 1,
    Edited = 2,
    Rejected = 3
}
