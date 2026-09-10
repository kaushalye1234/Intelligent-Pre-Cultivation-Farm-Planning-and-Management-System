using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Models.Inspections;

public sealed class CropIssue : AuditableEntity
{
    public Guid FieldInspectionId { get; set; }
    public FieldInspection? FieldInspection { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public CropIssueSeverity Severity { get; set; } = CropIssueSeverity.Low;
    public CropIssueStatus Status { get; set; } = CropIssueStatus.Open;
    public DateTime? EscalatedAt { get; set; }
    public Guid? EscalatedToUserId { get; set; }
}

public enum CropIssueSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum CropIssueStatus
{
    Open = 1,
    Escalated = 2,
    Resolved = 3,
    Closed = 4
}
