using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Validators.Shared;

namespace AgriAssist.Api.Validators.Resources;

public sealed class ResourceCategoryRequestValidator : IRequestValidator<ResourceCategoryRequest>
{
    public IReadOnlyList<string> Validate(ResourceCategoryRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120) errors.Add("Category name is required and must be 120 characters or fewer.");
        if (request.Description?.Length > 500) errors.Add("Category description must be 500 characters or fewer.");
        return errors;
    }
}

public sealed class SupplierRequestValidator : IRequestValidator<SupplierRequest>
{
    public IReadOnlyList<string> Validate(SupplierRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 160) errors.Add("Supplier name is required and must be 160 characters or fewer.");
        if (request.ContactEmail.Length > 180 || (!string.IsNullOrWhiteSpace(request.ContactEmail) && !request.ContactEmail.Contains('@'))) errors.Add("Supplier email must be valid.");
        if (request.Phone.Length > 40) errors.Add("Supplier phone must be 40 characters or fewer.");
        return errors;
    }
}

public sealed class ResourceRequestValidator : IRequestValidator<ResourceRequest>
{
    public IReadOnlyList<string> Validate(ResourceRequest request)
    {
        var errors = new List<string>();
        if (request.ResourceCategoryId == Guid.Empty) errors.Add("Resource category is required.");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 160) errors.Add("Resource name is required and must be 160 characters or fewer.");
        if (string.IsNullOrWhiteSpace(request.Unit) || request.Unit.Length > 40) errors.Add("Resource unit is required and must be 40 characters or fewer.");
        return errors;
    }
}

public sealed class InventoryStockRequestValidator : IRequestValidator<InventoryStockRequest>
{
    public IReadOnlyList<string> Validate(InventoryStockRequest request)
    {
        var errors = new List<string>();
        if (request.ResourceId == Guid.Empty) errors.Add("Resource is required.");
        if (request.QuantityOnHand < 0) errors.Add("Quantity on hand cannot be negative.");
        if (request.LowStockThreshold < 0) errors.Add("Low-stock threshold cannot be negative.");
        return errors;
    }
}

public sealed class ResourceReservationRequestValidator : IRequestValidator<ResourceReservationRequest>
{
    public IReadOnlyList<string> Validate(ResourceReservationRequest request)
    {
        var errors = new List<string>();
        if (request.InventoryStockId == Guid.Empty) errors.Add("Inventory stock is required.");
        if (request.Quantity <= 0) errors.Add("Reservation quantity must be positive.");
        if (string.IsNullOrWhiteSpace(request.Purpose) || request.Purpose.Length > 500) errors.Add("Reservation purpose is required and must be 500 characters or fewer.");
        return errors;
    }
}

public sealed class StockAdjustmentRequestValidator : IRequestValidator<StockAdjustmentRequest>
{
    public IReadOnlyList<string> Validate(StockAdjustmentRequest request)
    {
        var errors = new List<string>();
        if (request.Quantity <= 0) errors.Add("Stock adjustment quantity must be positive.");
        if (request.Note.Length > 500) errors.Add("Stock adjustment note must be 500 characters or fewer.");
        return errors;
    }
}
