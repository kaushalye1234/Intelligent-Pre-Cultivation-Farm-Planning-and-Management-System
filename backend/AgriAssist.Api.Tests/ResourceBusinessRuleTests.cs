using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.Resources;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.Resources;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.Resources;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Tests;

public sealed class ResourceBusinessRuleTests
{
    [Fact]
    public async Task Reserve_rejects_quantity_above_availability()
    {
        await using var db = NewDbContext();
        var service = NewService(db, new TestCurrentUserService());
        var stock = await SeedStockAsync(db, onHand: 5);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            service.ReserveAsync(new ResourceReservationRequest(stock.Id, 6, "Too much"), CancellationToken.None));

        Assert.Equal("INSUFFICIENT_STOCK", exception.Code);
    }

    [Fact]
    public async Task Reserve_updates_stock_row_version_and_records_a_transaction()
    {
        await using var db = NewDbContext();
        var service = NewService(db, new TestCurrentUserService());
        var stock = await SeedStockAsync(db, onHand: 10);
        var originalVersion = stock.RowVersion.ToArray();

        await service.ReserveAsync(new ResourceReservationRequest(stock.Id, 4, "Planting"), CancellationToken.None);

        var saved = await db.InventoryStocks.AsNoTracking().SingleAsync();
        Assert.Equal(4, saved.ReservedQuantity);
        Assert.Equal(6, saved.AvailableQuantity);
        Assert.NotEqual(originalVersion, saved.RowVersion);
        Assert.Single(db.StockTransactions, item => item.Type == StockTransactionType.Reserve && item.Quantity == 4);
    }

    [Fact]
    public async Task Release_returns_quantity_to_available_stock()
    {
        await using var db = NewDbContext();
        var service = NewService(db, new TestCurrentUserService());
        var stock = await SeedStockAsync(db, onHand: 10);
        var reservation = await service.ReserveAsync(new ResourceReservationRequest(stock.Id, 4, "Planting"), CancellationToken.None);

        var released = await service.ReleaseAsync(reservation.Id, CancellationToken.None);

        Assert.Equal(ResourceReservationStatus.Released, released.Status);
        Assert.Equal(0, (await db.InventoryStocks.AsNoTracking().SingleAsync()).ReservedQuantity);
        await Assert.ThrowsAsync<ApiException>(() => service.CancelReservationAsync(reservation.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Farmer_cannot_cancel_another_users_reservation()
    {
        await using var db = NewDbContext();
        var stock = await SeedStockAsync(db, onHand: 10);
        var officerService = NewService(db, new TestCurrentUserService());
        var reservation = await officerService.ReserveAsync(new ResourceReservationRequest(stock.Id, 2, "Officer reservation"), CancellationToken.None);
        var farmerService = NewService(db, new TestCurrentUserService(ApplicationRole.Farmer));

        var exception = await Assert.ThrowsAsync<ApiException>(() => farmerService.CancelReservationAsync(reservation.Id, CancellationToken.None));

        Assert.Equal("RESERVATION_FORBIDDEN", exception.Code);
        Assert.Empty((await farmerService.SearchReservationsAsync(new PagedQuery(), null, CancellationToken.None)).Items);
        Assert.Single((await officerService.SearchReservationsAsync(new PagedQuery(), ResourceReservationStatus.Active, CancellationToken.None)).Items);
    }

    [Fact]
    public async Task Upsert_stock_records_add_and_remove_transactions()
    {
        await using var db = NewDbContext();
        var service = NewService(db, new TestCurrentUserService());
        var stock = await SeedStockAsync(db, onHand: 10);

        await service.UpsertStockAsync(new InventoryStockRequest(stock.ResourceId, 15, 2), CancellationToken.None);
        await service.UpsertStockAsync(new InventoryStockRequest(stock.ResourceId, 12, 2), CancellationToken.None);

        var history = await service.GetStockHistoryAsync(stock.Id, CancellationToken.None);
        Assert.Contains(history, item => item.Type == StockTransactionType.Add && item.Quantity == 5);
        Assert.Contains(history, item => item.Type == StockTransactionType.Remove && item.Quantity == 3);
    }

    [Fact]
    public async Task Upsert_stock_rejects_quantity_below_reserved()
    {
        await using var db = NewDbContext();
        var service = NewService(db, new TestCurrentUserService());
        var stock = await SeedStockAsync(db, onHand: 10);
        await service.ReserveAsync(new ResourceReservationRequest(stock.Id, 8, "Planting"), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            service.UpsertStockAsync(new InventoryStockRequest(stock.ResourceId, 5, 1), CancellationToken.None));

        Assert.Equal("STOCK_BELOW_RESERVED", exception.Code);
    }

    [Fact]
    public async Task Concurrent_stock_edit_is_rejected_instead_of_overwritten()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var seedDb = NewDbContext(databaseName);
        var stock = await SeedStockAsync(seedDb, onHand: 10);

        // Two users load the same stock row, then both try to reserve from it.
        await using var firstDb = NewDbContext(databaseName);
        await using var secondDb = NewDbContext(databaseName);
        var firstStock = await firstDb.InventoryStocks.SingleAsync();
        var secondStock = await secondDb.InventoryStocks.SingleAsync();

        await NewService(firstDb, new TestCurrentUserService()).ReserveAsync(new ResourceReservationRequest(stock.Id, 6, "First"), CancellationToken.None);
        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            NewService(secondDb, new TestCurrentUserService()).ReserveAsync(new ResourceReservationRequest(stock.Id, 6, "Second"), CancellationToken.None));

        Assert.Equal("STOCK_CHANGED", exception.Code);
        await using var checkDb = NewDbContext(databaseName);
        Assert.Equal(6, (await checkDb.InventoryStocks.SingleAsync()).ReservedQuantity);
    }

    private static AppDbContext NewDbContext(string? databaseName = null) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString()).Options);

    private static ResourceService NewService(AppDbContext db, TestCurrentUserService currentUser) =>
        new(
            db,
            currentUser,
            new ResourceCategoryRequestValidator(),
            new SupplierRequestValidator(),
            new ResourceRequestValidator(),
            new InventoryStockRequestValidator(),
            new ResourceReservationRequestValidator());

    private static async Task<InventoryStock> SeedStockAsync(AppDbContext db, decimal onHand)
    {
        var category = new ResourceCategory { Name = "Seed" };
        var resource = new Resource { Name = "Seed Pack", Unit = "kg", ResourceCategory = category };
        var stock = new InventoryStock { Resource = resource, QuantityOnHand = onHand, ReservedQuantity = 0, LowStockThreshold = 1, RowVersion = Guid.NewGuid().ToByteArray() };
        db.InventoryStocks.Add(stock);
        await db.SaveChangesAsync();
        return stock;
    }
}
