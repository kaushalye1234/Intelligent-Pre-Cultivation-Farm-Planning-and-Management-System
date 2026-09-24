using AgriAssist.Api.Models.Resources;

namespace AgriAssist.Api.Dtos.Resources;

public sealed record ResourceCategoryRequest(string Name, string? Description);
public sealed record ResourceCategoryResponse(Guid Id, string Name, string? Description);

public sealed record SupplierRequest(string Name, string ContactEmail, string Phone);
public sealed record SupplierResponse(Guid Id, string Name, string ContactEmail, string Phone);

public sealed record ResourceRequest(Guid ResourceCategoryId, Guid? SupplierId, string Name, string Unit, bool IsActive);
public sealed record ResourceResponse(Guid Id, Guid ResourceCategoryId, Guid? SupplierId, string Name, string Unit, bool IsActive);

public sealed record InventoryStockRequest(Guid ResourceId, decimal QuantityOnHand, decimal LowStockThreshold);
public sealed record InventoryStockResponse(Guid Id, Guid ResourceId, decimal QuantityOnHand, decimal ReservedQuantity, decimal AvailableQuantity, decimal LowStockThreshold, string ResourceName, string Unit);

public sealed record StockAdjustmentRequest(decimal Quantity, string Note);
public sealed record StockTransactionResponse(Guid Id, Guid InventoryStockId, StockTransactionType Type, decimal Quantity, string Note, DateTime CreatedAt);

public sealed record ResourceReservationRequest(Guid InventoryStockId, decimal Quantity, string Purpose);
public sealed record ResourceReservationResponse(Guid Id, Guid InventoryStockId, Guid RequestedByUserId, decimal Quantity, ResourceReservationStatus Status, DateTime? ReleasedAt, string Purpose, string ResourceName, string Unit, DateTime CreatedAt);
