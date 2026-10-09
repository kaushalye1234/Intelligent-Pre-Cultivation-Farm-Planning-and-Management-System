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
    public CropPlanHistoryAction Action { get; set; } = CropPlanHistoryAction.StatusChanged;
    public ApplicationRole? ChangedByRole { get; set; }
    public string? Reason { get; set; }
}

public enum CropPlanHistoryAction
{
    StatusChanged = 1,
    Cancelled = 2,
    Archived = 3
}
