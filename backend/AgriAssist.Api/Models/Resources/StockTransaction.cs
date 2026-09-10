using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Models.Resources;

public sealed class StockTransaction : AuditableEntity
{
    public Guid InventoryStockId { get; set; }
    public InventoryStock? InventoryStock { get; set; }
    public StockTransactionType Type { get; set; }
    public decimal Quantity { get; set; }
    public string Note { get; set; } = string.Empty;
}

public enum StockTransactionType
{
    Add = 1,
    Remove = 2,
    Reserve = 3,
    Release = 4
}
