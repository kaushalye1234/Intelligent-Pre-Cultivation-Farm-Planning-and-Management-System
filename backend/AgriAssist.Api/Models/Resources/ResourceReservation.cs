using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Models.Resources;

public sealed class ResourceReservation : AuditableEntity
{
    public Guid InventoryStockId { get; set; }
    public InventoryStock? InventoryStock { get; set; }
    public Guid RequestedByUserId { get; set; }
    public AppUser? RequestedByUser { get; set; }
    public decimal Quantity { get; set; }
    public ResourceReservationStatus Status { get; set; } = ResourceReservationStatus.Active;
    public DateTime? ReleasedAt { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public Guid? GeneratedByWorkflowId { get; set; }
    public AgentWorkflow? GeneratedByWorkflow { get; set; }
    public int? CandidateRevision { get; set; }
}

public enum ResourceReservationStatus
{
    Active = 1,
    Released = 2,
    Cancelled = 3
}
