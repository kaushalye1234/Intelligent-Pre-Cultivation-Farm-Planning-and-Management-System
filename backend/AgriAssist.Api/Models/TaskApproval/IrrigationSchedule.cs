using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Models.TaskApproval;

public sealed class IrrigationSchedule : AuditableEntity
{
    public Guid FieldId { get; set; }
    public Field? Field { get; set; }
    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; }
    public IrrigationScheduleStatus Status { get; set; } = IrrigationScheduleStatus.PendingApproval;
    public string Notes { get; set; } = string.Empty;
}

public enum IrrigationScheduleStatus
{
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    RevisionRequested = 4,
    Completed = 5,
    Cancelled = 6
}
