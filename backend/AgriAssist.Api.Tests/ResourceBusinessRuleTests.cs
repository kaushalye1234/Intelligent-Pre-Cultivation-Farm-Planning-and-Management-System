using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Models.Resources;
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
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var currentUser = new TestCurrentUserService();
        var service = new ResourceService(
            db,
            currentUser,
            new ResourceCategoryRequestValidator(),
            new SupplierRequestValidator(),
            new ResourceRequestValidator(),
            new InventoryStockRequestValidator(),
            new ResourceReservationRequestValidator());

        var category = new ResourceCategory { Name = "Seed" };
        var resource = new Resource { Name = "Seed Pack", Unit = "kg", ResourceCategory = category };
        var stock = new InventoryStock { Resource = resource, QuantityOnHand = 5, ReservedQuantity = 0, LowStockThreshold = 1 };
        db.InventoryStocks.Add(stock);
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            service.ReserveAsync(new ResourceReservationRequest(stock.Id, 6, "Too much"), CancellationToken.None));

        Assert.Equal("INSUFFICIENT_STOCK", exception.Code);
    }
}
