using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Models.CropPlanning;

public sealed class CropCycle : AuditableEntity
{
    public Guid FieldId { get; set; }
    public Field? Field { get; set; }
    public Guid CropTypeId { get; set; }
    public CropType? CropType { get; set; }
    public DateOnly PlannedStartDate { get; set; }
    public DateOnly PlannedEndDate { get; set; }
    public CropCycleStatus Status { get; set; } = CropCycleStatus.Planned;
}

public enum CropCycleStatus
{
    Planned = 1,
    Active = 2,
    Completed = 3,
    Cancelled = 4
}
