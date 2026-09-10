using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Models.CropPlanning;

public sealed class CropPlanRequestHistory : AuditableEntity
{
    public Guid CropPlanRequestId { get; set; }
    public CropPlanRequest? CropPlanRequest { get; set; }
    public CropPlanRequestStatus FromStatus { get; set; }
    public CropPlanRequestStatus ToStatus { get; set; }
    public string Note { get; set; } = string.Empty;
    public Guid ChangedByUserId { get; set; }
    public AppUser? ChangedByUser { get; set; }
}
