using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Models.Inspections;

public sealed class FollowUpRecommendation : AuditableEntity
{
    public Guid CropIssueId { get; set; }
    public CropIssue? CropIssue { get; set; }
    public string Recommendation { get; set; } = string.Empty;
    public DateTime? DueAt { get; set; }
    public bool IsCompleted { get; set; }
}
