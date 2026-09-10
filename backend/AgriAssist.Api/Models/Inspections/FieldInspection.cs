using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Models.Inspections;

public sealed class FieldInspection : AuditableEntity
{
    public Guid FieldId { get; set; }
    public Field? Field { get; set; }
    public Guid InspectorUserId { get; set; }
    public AppUser? InspectorUser { get; set; }
    public DateTime ScheduledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public InspectionStatus Status { get; set; } = InspectionStatus.Scheduled;
    public string Summary { get; set; } = string.Empty;
}

public enum InspectionStatus
{
    Scheduled = 1,
    InProgress = 2,
    Completed = 3,
    Escalated = 4,
    Cancelled = 5
}
