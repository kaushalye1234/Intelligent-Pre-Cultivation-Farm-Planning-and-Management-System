using System.Net;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.Resources;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AgriAssist.Api.Services.Resources;

public sealed class ResourceService(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    IRequestValidator<ResourceCategoryRequest> categoryValidator,
    IRequestValidator<SupplierRequest> supplierValidator,
    IRequestValidator<ResourceRequest> resourceValidator,
    IRequestValidator<InventoryStockRequest> stockValidator,
    IRequestValidator<ResourceReservationRequest> reservationValidator) : IResourceService
{
    public async Task<PagedResult<ResourceCategoryResponse>> SearchCategoriesAsync(PagedQuery query, CancellationToken cancellationToken)
    {
        query.Normalize();
        var categories = dbContext.ResourceCategories.AsNoTracking().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Search)) categories = categories.Where(item => item.Name.ToLower().Contains(query.Search.ToLower()));
        categories = categories.OrderBy(item => item.Name);
        var total = await categories.CountAsync(cancellationToken);
        var items = await categories.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(item => new ResourceCategoryResponse(item.Id, item.Name, item.Description)).ToListAsync(cancellationToken);
        return new PagedResult<ResourceCategoryResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<ResourceCategoryResponse> CreateCategoryAsync(ResourceCategoryRequest request, CancellationToken cancellationToken)
    {
        Validate(categoryValidator.Validate(request));
        var category = new ResourceCategory { Name = request.Name.Trim(), Description = request.Description?.Trim(), CreatedByUserId = currentUser.UserId };
        dbContext.ResourceCategories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ResourceCategoryResponse(category.Id, category.Name, category.Description);
    }

    public async Task<PagedResult<SupplierResponse>> SearchSuppliersAsync(PagedQuery query, CancellationToken cancellationToken)
    {
        query.Normalize();
        var suppliers = dbContext.Suppliers.AsNoTracking().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Search)) suppliers = suppliers.Where(item => item.Name.ToLower().Contains(query.Search.ToLower()));
        suppliers = suppliers.OrderBy(item => item.Name);
        var total = await suppliers.CountAsync(cancellationToken);
        var items = await suppliers.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(item => new SupplierResponse(item.Id, item.Name, item.ContactEmail, item.Phone)).ToListAsync(cancellationToken);
        return new PagedResult<SupplierResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<SupplierResponse> CreateSupplierAsync(SupplierRequest request, CancellationToken cancellationToken)
    {
        Validate(supplierValidator.Validate(request));
        var supplier = new Supplier { Name = request.Name.Trim(), ContactEmail = request.ContactEmail.Trim(), Phone = request.Phone.Trim(), CreatedByUserId = currentUser.UserId };
        dbContext.Suppliers.Add(supplier);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new SupplierResponse(supplier.Id, supplier.Name, supplier.ContactEmail, supplier.Phone);
    }

    public async Task<PagedResult<ResourceResponse>> SearchResourcesAsync(PagedQuery query, CancellationToken cancellationToken)
    {
        query.Normalize();
        var resources = dbContext.Resources.AsNoTracking().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Search)) resources = resources.Where(item => item.Name.ToLower().Contains(query.Search.ToLower()));
        resources = resources.OrderBy(item => item.Name);
        var total = await resources.CountAsync(cancellationToken);
        var items = await resources.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(item => MapResource(item)).ToListAsync(cancellationToken);
        return new PagedResult<ResourceResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<ResourceResponse> CreateResourceAsync(ResourceRequest request, CancellationToken cancellationToken)
    {
        Validate(resourceValidator.Validate(request));
        if (!await dbContext.ResourceCategories.AnyAsync(item => item.Id == request.ResourceCategoryId && !item.IsDeleted, cancellationToken)) throw NotFound("Resource category");
        if (request.SupplierId.HasValue && !await dbContext.Suppliers.AnyAsync(item => item.Id == request.SupplierId && !item.IsDeleted, cancellationToken)) throw NotFound("Supplier");
        var resource = new Resource { ResourceCategoryId = request.ResourceCategoryId, SupplierId = request.SupplierId, Name = request.Name.Trim(), Unit = request.Unit.Trim(), IsActive = request.IsActive, CreatedByUserId = currentUser.UserId };
        dbContext.Resources.Add(resource);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapResource(resource);
    }

    public async Task<InventoryStockResponse> UpsertStockAsync(InventoryStockRequest request, CancellationToken cancellationToken)
    {
        Validate(stockValidator.Validate(request));
        if (!await dbContext.Resources.AnyAsync(item => item.Id == request.ResourceId && item.IsActive && !item.IsDeleted, cancellationToken)) throw NotFound("Resource");
        var stock = await dbContext.InventoryStocks.SingleOrDefaultAsync(item => item.ResourceId == request.ResourceId, cancellationToken);
        var previousQuantity = stock?.QuantityOnHand ?? 0;
        if (stock is null)
        {
            stock = new InventoryStock { ResourceId = request.ResourceId, QuantityOnHand = request.QuantityOnHand, LowStockThreshold = request.LowStockThreshold, RowVersion = NewRowVersion(), CreatedByUserId = currentUser.UserId };
            dbContext.InventoryStocks.Add(stock);
        }
        else
        {
            if (request.QuantityOnHand < stock.ReservedQuantity) throw new ApiException(HttpStatusCode.Conflict, "STOCK_BELOW_RESERVED", "Quantity on hand cannot be less than reserved quantity.");
            stock.QuantityOnHand = request.QuantityOnHand;
            stock.LowStockThreshold = request.LowStockThreshold;
            stock.RowVersion = NewRowVersion();
            stock.UpdatedAt = DateTime.UtcNow;
        }

        // Keep the stock ledger complete: every change to quantity on hand is recorded.
        var change = request.QuantityOnHand - previousQuantity;
        if (change != 0)
        {
            dbContext.StockTransactions.Add(new StockTransaction
            {
                InventoryStock = stock,
                Type = change > 0 ? StockTransactionType.Add : StockTransactionType.Remove,
                Quantity = Math.Abs(change),
                Note = "Stock level updated.",
                CreatedByUserId = currentUser.UserId
            });
        }

        await SaveStockChangesAsync(cancellationToken);
        return MapStock(stock);
    }

    public async Task<PagedResult<InventoryStockResponse>> SearchStocksAsync(PagedQuery query, bool? lowStockOnly, CancellationToken cancellationToken)
    {
        query.Normalize();
        var stocks = dbContext.InventoryStocks.AsNoTracking().Where(item => !item.IsDeleted);
        if (lowStockOnly == true) stocks = stocks.Where(item => item.QuantityOnHand - item.ReservedQuantity <= item.LowStockThreshold);
        stocks = stocks.OrderBy(item => item.ResourceId);
        var total = await stocks.CountAsync(cancellationToken);
        var items = await stocks.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(item => MapStock(item)).ToListAsync(cancellationToken);
        return new PagedResult<InventoryStockResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<IReadOnlyList<StockTransactionResponse>> GetStockHistoryAsync(Guid stockId, CancellationToken cancellationToken)
    {
        return await dbContext.StockTransactions.AsNoTracking().Where(item => item.InventoryStockId == stockId).OrderBy(item => item.CreatedAt).Select(item => MapTransaction(item)).ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<ResourceReservationResponse>> SearchReservationsAsync(PagedQuery query, ResourceReservationStatus? status, CancellationToken cancellationToken)
    {
        query.Normalize();
        var reservations = dbContext.ResourceReservations.AsNoTracking().Where(item => !item.IsDeleted);
        if (!IsResourceManager()) reservations = reservations.Where(item => item.RequestedByUserId == currentUser.UserId);
        if (status.HasValue) reservations = reservations.Where(item => item.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(query.Search)) reservations = reservations.Where(item => item.Purpose.ToLower().Contains(query.Search.ToLower()));
        reservations = reservations.OrderByDescending(item => item.CreatedAt);
        var total = await reservations.CountAsync(cancellationToken);
        var items = await reservations.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(item => MapReservation(item)).ToListAsync(cancellationToken);
        return new PagedResult<ResourceReservationResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<ResourceReservationResponse> ReserveAsync(ResourceReservationRequest request, CancellationToken cancellationToken)
    {
        Validate(reservationValidator.Validate(request));
        await using var transaction = await BeginTransactionIfRelationalAsync(cancellationToken);
        var stock = await dbContext.InventoryStocks.SingleOrDefaultAsync(item => item.Id == request.InventoryStockId && !item.IsDeleted, cancellationToken) ?? throw NotFound("Inventory stock");
        if (stock.AvailableQuantity < request.Quantity) throw new ApiException(HttpStatusCode.Conflict, "INSUFFICIENT_STOCK", "Reservation cannot exceed available stock.");
        stock.ReservedQuantity += request.Quantity;
        stock.RowVersion = NewRowVersion();
        stock.UpdatedAt = DateTime.UtcNow;

        var reservation = new ResourceReservation { InventoryStockId = stock.Id, RequestedByUserId = RequireUser(), Quantity = request.Quantity, Purpose = request.Purpose.Trim(), CreatedByUserId = currentUser.UserId };
        dbContext.ResourceReservations.Add(reservation);
        dbContext.StockTransactions.Add(new StockTransaction { InventoryStockId = stock.Id, Type = StockTransactionType.Reserve, Quantity = request.Quantity, Note = request.Purpose.Trim(), CreatedByUserId = currentUser.UserId });
        await SaveStockChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return MapReservation(reservation);
    }

    public Task<ResourceReservationResponse> ReleaseAsync(Guid reservationId, CancellationToken cancellationToken) =>
        CloseReservationAsync(reservationId, ResourceReservationStatus.Released, cancellationToken);

    public Task<ResourceReservationResponse> CancelReservationAsync(Guid reservationId, CancellationToken cancellationToken) =>
        CloseReservationAsync(reservationId, ResourceReservationStatus.Cancelled, cancellationToken);

    private async Task<ResourceReservationResponse> CloseReservationAsync(Guid reservationId, ResourceReservationStatus newStatus, CancellationToken cancellationToken)
    {
        var action = newStatus == ResourceReservationStatus.Released ? "released" : "cancelled";
        await using var transaction = await BeginTransactionIfRelationalAsync(cancellationToken);
        var reservation = await dbContext.ResourceReservations.Include(item => item.InventoryStock).SingleOrDefaultAsync(item => item.Id == reservationId && !item.IsDeleted, cancellationToken) ?? throw NotFound("Reservation");
        if (!IsResourceManager() && reservation.RequestedByUserId != currentUser.UserId)
        {
            throw new ApiException(HttpStatusCode.Forbidden, "RESERVATION_FORBIDDEN", $"Only the requester or a resource officer can have this reservation {action}.");
        }

        if (reservation.Status != ResourceReservationStatus.Active) throw new ApiException(HttpStatusCode.Conflict, "RESERVATION_NOT_ACTIVE", $"Only active reservations can be {action}.");
        var stock = reservation.InventoryStock ?? throw NotFound("Inventory stock");
        if (stock.ReservedQuantity < reservation.Quantity) throw new ApiException(HttpStatusCode.Conflict, "NEGATIVE_RESERVED_STOCK", "Reserved stock cannot be negative.");
        stock.ReservedQuantity -= reservation.Quantity;
        stock.RowVersion = NewRowVersion();
        stock.UpdatedAt = DateTime.UtcNow;
        reservation.Status = newStatus;
        reservation.ReleasedAt = DateTime.UtcNow;
        dbContext.StockTransactions.Add(new StockTransaction { InventoryStockId = stock.Id, Type = StockTransactionType.Release, Quantity = reservation.Quantity, Note = $"Reservation {action}.", CreatedByUserId = currentUser.UserId });
        await SaveStockChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return MapReservation(reservation);
    }

    /// <summary>
    /// InventoryStock.RowVersion is a concurrency token and is changed on every write, so if two users
    /// change the same stock at the same time the second save fails instead of silently overwriting.
    /// </summary>
    private async Task SaveStockChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ApiException(HttpStatusCode.Conflict, "STOCK_CHANGED", "This stock was changed by another user. Refresh and try again.");
        }
    }

    private bool IsResourceManager() => currentUser.Role is ApplicationRole.ResourceOfficer or ApplicationRole.Admin;
    private static byte[] NewRowVersion() => Guid.NewGuid().ToByteArray();

    private async Task<IDbContextTransaction?> BeginTransactionIfRelationalAsync(CancellationToken cancellationToken)
    {
        return dbContext.Database.IsRelational() ? await dbContext.Database.BeginTransactionAsync(cancellationToken) : null;
    }

    private Guid RequireUser() => currentUser.UserId ?? throw new ApiException(HttpStatusCode.Unauthorized, "AUTH_REQUIRED", "Authentication is required.");
    private static ApiException NotFound(string name) => new(HttpStatusCode.NotFound, "NOT_FOUND", $"{name} was not found.");
    private static void Validate(IReadOnlyList<string> errors) { if (errors.Count > 0) throw new ApiException(HttpStatusCode.BadRequest, "VALIDATION_ERROR", string.Join(" ", errors)); }
    private static ResourceResponse MapResource(Resource item) => new(item.Id, item.ResourceCategoryId, item.SupplierId, item.Name, item.Unit, item.IsActive);
    private static InventoryStockResponse MapStock(InventoryStock item) => new(item.Id, item.ResourceId, item.QuantityOnHand, item.ReservedQuantity, item.AvailableQuantity, item.LowStockThreshold);
    private static StockTransactionResponse MapTransaction(StockTransaction item) => new(item.Id, item.InventoryStockId, item.Type, item.Quantity, item.Note, item.CreatedAt);
    private static ResourceReservationResponse MapReservation(ResourceReservation item) => new(item.Id, item.InventoryStockId, item.RequestedByUserId, item.Quantity, item.Status, item.ReleasedAt, item.Purpose);
}
