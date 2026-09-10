using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Models.Resources;

public sealed class InventoryStock : AuditableEntity
{
    public Guid ResourceId { get; set; }
    public Resource? Resource { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal LowStockThreshold { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public decimal AvailableQuantity => QuantityOnHand - ReservedQuantity;
}
