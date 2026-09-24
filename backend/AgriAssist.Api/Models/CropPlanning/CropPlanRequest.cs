using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Models.CropPlanning;

public sealed class CropPlanRequest : AuditableEntity
{
    public Guid FarmId { get; set; }
    public Farm? Farm { get; set; }
    public Guid? FieldId { get; set; }
    public Field? Field { get; set; }
    public Guid CropTypeId { get; set; }
    public CropType? CropType { get; set; }
    public Guid? CropVarietyId { get; set; }
    public CropVariety? CropVariety { get; set; }
    public CultivationSeason CultivationSeason { get; set; } = CultivationSeason.NotSure;
    public Guid? PreviousCropTypeId { get; set; }
    public CropType? PreviousCropType { get; set; }
    public string PreviousKnownProblemsJson { get; set; } = "[]";
    public Guid RequestedByUserId { get; set; }
    public AppUser? RequestedByUser { get; set; }
    public DateOnly PreferredStartDate { get; set; }
    public DateOnly PreferredEndDate { get; set; }
    public decimal Budget { get; set; }
    public string Objective { get; set; } = string.Empty;
    public CropPlanRequestStatus Status { get; set; } = CropPlanRequestStatus.Draft;
    public List<CropPlanRequestHistory> History { get; set; } = [];
}

public enum CultivationSeason
{
    NotSure = 0,
    Maha = 1,
    Yala = 2,
    OffSeason = 3
}

public enum CropPlanRequestStatus
{
    Draft = 1,
    Submitted = 2,
    PreliminaryGenerated = 3,
    Approved = 4,
    Rejected = 5,
    Cancelled = 6
}
