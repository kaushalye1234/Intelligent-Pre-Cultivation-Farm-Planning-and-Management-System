using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.Resources;
using AgriAssist.Api.Services.Resources;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.Resources;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Tests;

/// <summary>Service-level tests for resource/category/supplier update + delete, filtering and sorting (EF InMemory).</summary>
public sealed class ResourceCrudFilterSortTests
{
    // ---- Update / delete ----

    [Fact]
    public async Task Update_resource_changes_fields_and_rejects_invalid_input()
    {
        await using var db = NewDbContext();
        var service = NewService(db);
        var category = await service.CreateCategoryAsync(new ResourceCategoryRequest("Seeds", null), CancellationToken.None);
        var resource = await service.CreateResourceAsync(new ResourceRequest(category.Id, null, "Paddy Seed", "kg", true), CancellationToken.None);
        var newCategory = await service.CreateCategoryAsync(new ResourceCategoryRequest("Fertiliser", null), CancellationToken.None);
        var supplier = await service.CreateSupplierAsync(new SupplierRequest("AgroCo", "sales@agroco.test", "0112223333"), CancellationToken.None);

        var updated = await service.UpdateResourceAsync(resource.Id, new ResourceRequest(newCategory.Id, supplier.Id, "  Urea  ", "bag", false), CancellationToken.None);

        Assert.Equal("Urea", updated.Name);
        Assert.Equal("bag", updated.Unit);
        Assert.False(updated.IsActive);
        Assert.Equal(newCategory.Id, updated.ResourceCategoryId);
        Assert.Equal(supplier.Id, updated.SupplierId);
        Assert.NotNull((await db.Resources.AsNoTracking().SingleAsync()).UpdatedByUserId);

        var invalid = await Assert.ThrowsAsync<ApiException>(() => service.UpdateResourceAsync(resource.Id, new ResourceRequest(newCategory.Id, null, " ", "kg", true), CancellationToken.None));
        Assert.Equal("VALIDATION_ERROR", invalid.Code);
        var badCategory = await Assert.ThrowsAsync<ApiException>(() => service.UpdateResourceAsync(resource.Id, new ResourceRequest(Guid.NewGuid(), null, "Urea", "kg", true), CancellationToken.None));
        Assert.Equal("NOT_FOUND", badCategory.Code);
        var badSupplier = await Assert.ThrowsAsync<ApiException>(() => service.UpdateResourceAsync(resource.Id, new ResourceRequest(newCategory.Id, Guid.NewGuid(), "Urea", "kg", true), CancellationToken.None));
        Assert.Equal("NOT_FOUND", badSupplier.Code);
        var missing = await Assert.ThrowsAsync<ApiException>(() => service.UpdateResourceAsync(Guid.NewGuid(), new ResourceRequest(newCategory.Id, null, "Urea", "kg", true), CancellationToken.None));
        Assert.Equal("NOT_FOUND", missing.Code);
    }

    [Fact]
    public async Task Delete_resource_soft_deletes_it_and_its_stock()
    {
        await using var db = NewDbContext();
        var service = NewService(db);
        var stock = await SeedStockAsync(service, "Paddy Seed", onHand: 5);

        await service.DeleteResourceAsync(stock.ResourceId, CancellationToken.None);

        Assert.True((await db.Resources.SingleAsync()).IsDeleted);
        Assert.True((await db.InventoryStocks.SingleAsync()).IsDeleted);
        Assert.Empty((await service.SearchResourcesAsync(new PagedQuery(), null, null, CancellationToken.None)).Items);
        Assert.Empty((await service.SearchStocksAsync(new PagedQuery(), null, CancellationToken.None)).Items);
        var again = await Assert.ThrowsAsync<ApiException>(() => service.DeleteResourceAsync(stock.ResourceId, CancellationToken.None));
        Assert.Equal("NOT_FOUND", again.Code);
        var reserve = await Assert.ThrowsAsync<ApiException>(() => service.ReserveAsync(new ResourceReservationRequest(stock.Id, 1, "After delete"), CancellationToken.None));
        Assert.Equal("NOT_FOUND", reserve.Code);
    }

    [Fact]
    public async Task Delete_resource_is_blocked_while_reservations_are_active()
    {
        await using var db = NewDbContext();
        var service = NewService(db);
        var stock = await SeedStockAsync(service, "Paddy Seed", onHand: 10);
        var reservation = await service.ReserveAsync(new ResourceReservationRequest(stock.Id, 3, "Planting"), CancellationToken.None);

        var blocked = await Assert.ThrowsAsync<ApiException>(() => service.DeleteResourceAsync(stock.ResourceId, CancellationToken.None));

        Assert.Equal("RESOURCE_HAS_ACTIVE_RESERVATIONS", blocked.Code);
        Assert.False((await db.Resources.AsNoTracking().SingleAsync()).IsDeleted);
        Assert.Equal(3, (await db.InventoryStocks.AsNoTracking().SingleAsync()).ReservedQuantity);

        await service.ReleaseAsync(reservation.Id, CancellationToken.None);
        await service.DeleteResourceAsync(stock.ResourceId, CancellationToken.None);
        Assert.True((await db.Resources.AsNoTracking().SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task Category_can_be_updated_and_deleted_and_is_protected_when_in_use_or_duplicated()
    {
        await using var db = NewDbContext();
        var service = NewService(db);
        var first = await service.CreateCategoryAsync(new ResourceCategoryRequest("Seeds", "Planting material"), CancellationToken.None);
        var second = await service.CreateCategoryAsync(new ResourceCategoryRequest("Tools", null), CancellationToken.None);

        var updated = await service.UpdateCategoryAsync(first.Id, new ResourceCategoryRequest(" Seeds and Saplings ", "Updated"), CancellationToken.None);
        Assert.Equal("Seeds and Saplings", updated.Name);
        Assert.Equal("Updated", updated.Description);

        var duplicate = await Assert.ThrowsAsync<ApiException>(() => service.UpdateCategoryAsync(second.Id, new ResourceCategoryRequest("seeds and saplings", null), CancellationToken.None));
        Assert.Equal("CATEGORY_NAME_EXISTS", duplicate.Code);
        var duplicateCreate = await Assert.ThrowsAsync<ApiException>(() => service.CreateCategoryAsync(new ResourceCategoryRequest("TOOLS", null), CancellationToken.None));
        Assert.Equal("CATEGORY_NAME_EXISTS", duplicateCreate.Code);
        var invalid = await Assert.ThrowsAsync<ApiException>(() => service.UpdateCategoryAsync(first.Id, new ResourceCategoryRequest("", null), CancellationToken.None));
        Assert.Equal("VALIDATION_ERROR", invalid.Code);
        // Saving a category under its own name is not a duplicate.
        await service.UpdateCategoryAsync(second.Id, new ResourceCategoryRequest("Tools", "Same name"), CancellationToken.None);

        await service.CreateResourceAsync(new ResourceRequest(second.Id, null, "Spade", "unit", true), CancellationToken.None);
        var inUse = await Assert.ThrowsAsync<ApiException>(() => service.DeleteCategoryAsync(second.Id, CancellationToken.None));
        Assert.Equal("CATEGORY_IN_USE", inUse.Code);

        await service.DeleteCategoryAsync(first.Id, CancellationToken.None);
        Assert.DoesNotContain((await service.SearchCategoriesAsync(new PagedQuery(), CancellationToken.None)).Items, item => item.Id == first.Id);
        var missing = await Assert.ThrowsAsync<ApiException>(() => service.UpdateCategoryAsync(first.Id, new ResourceCategoryRequest("Again", null), CancellationToken.None));
        Assert.Equal("NOT_FOUND", missing.Code);
    }

    [Fact]
    public async Task Supplier_can_be_updated_and_deleted_and_is_protected_when_in_use()
    {
        await using var db = NewDbContext();
        var service = NewService(db);
        var category = await service.CreateCategoryAsync(new ResourceCategoryRequest("Seeds", null), CancellationToken.None);
        var used = await service.CreateSupplierAsync(new SupplierRequest("Used Supplier", "used@test.example", "0111"), CancellationToken.None);
        var unused = await service.CreateSupplierAsync(new SupplierRequest("Unused Supplier", "unused@test.example", "0222"), CancellationToken.None);
        await service.CreateResourceAsync(new ResourceRequest(category.Id, used.Id, "Paddy Seed", "kg", true), CancellationToken.None);

        var updated = await service.UpdateSupplierAsync(unused.Id, new SupplierRequest(" Renamed ", "new@test.example", "0333"), CancellationToken.None);
        Assert.Equal("Renamed", updated.Name);
        Assert.Equal("new@test.example", updated.ContactEmail);
        var invalid = await Assert.ThrowsAsync<ApiException>(() => service.UpdateSupplierAsync(unused.Id, new SupplierRequest("Renamed", "not-an-email", "0333"), CancellationToken.None));
        Assert.Equal("VALIDATION_ERROR", invalid.Code);

        var inUse = await Assert.ThrowsAsync<ApiException>(() => service.DeleteSupplierAsync(used.Id, CancellationToken.None));
        Assert.Equal("SUPPLIER_IN_USE", inUse.Code);

        await service.DeleteSupplierAsync(unused.Id, CancellationToken.None);
        Assert.DoesNotContain((await service.SearchSuppliersAsync(new PagedQuery(), CancellationToken.None)).Items, item => item.Id == unused.Id);
        var missing = await Assert.ThrowsAsync<ApiException>(() => service.DeleteSupplierAsync(unused.Id, CancellationToken.None));
        Assert.Equal("NOT_FOUND", missing.Code);
    }

    // ---- Filtering ----

    [Fact]
    public async Task Resource_search_combines_text_category_and_supplier_filters_with_pagination()
    {
        await using var db = NewDbContext();
        var service = NewService(db);
        var seeds = await service.CreateCategoryAsync(new ResourceCategoryRequest("Seeds", null), CancellationToken.None);
        var tools = await service.CreateCategoryAsync(new ResourceCategoryRequest("Tools", null), CancellationToken.None);
        var acme = await service.CreateSupplierAsync(new SupplierRequest("Acme", "a@test.example", "1"), CancellationToken.None);
        var beta = await service.CreateSupplierAsync(new SupplierRequest("Beta", "b@test.example", "2"), CancellationToken.None);
        await service.CreateResourceAsync(new ResourceRequest(seeds.Id, acme.Id, "Paddy Seed", "kg", true), CancellationToken.None);
        await service.CreateResourceAsync(new ResourceRequest(seeds.Id, beta.Id, "Maize Seed", "kg", true), CancellationToken.None);
        await service.CreateResourceAsync(new ResourceRequest(seeds.Id, acme.Id, "Bean Seed", "kg", true), CancellationToken.None);
        await service.CreateResourceAsync(new ResourceRequest(tools.Id, acme.Id, "Paddy Sprayer", "unit", true), CancellationToken.None);

        var byCategory = await service.SearchResourcesAsync(new PagedQuery(), seeds.Id, null, CancellationToken.None);
        var bySupplier = await service.SearchResourcesAsync(new PagedQuery(), null, acme.Id, CancellationToken.None);
        var combined = await service.SearchResourcesAsync(new PagedQuery { Search = "paddy" }, seeds.Id, acme.Id, CancellationToken.None);
        var paged = await service.SearchResourcesAsync(new PagedQuery { Page = 2, PageSize = 2 }, null, acme.Id, CancellationToken.None);
        var unfiltered = await service.SearchResourcesAsync(new PagedQuery(), null, null, CancellationToken.None);

        Assert.Equal(["Bean Seed", "Maize Seed", "Paddy Seed"], byCategory.Items.Select(item => item.Name));
        Assert.Equal(["Bean Seed", "Paddy Seed", "Paddy Sprayer"], bySupplier.Items.Select(item => item.Name));
        Assert.Equal(["Paddy Seed"], combined.Items.Select(item => item.Name));
        Assert.Equal(3, paged.TotalCount);
        Assert.Equal(2, paged.TotalPages);
        Assert.Equal(["Paddy Sprayer"], paged.Items.Select(item => item.Name));
        Assert.Equal(4, unfiltered.TotalCount);
    }

    // ---- Sorting: ascending and descending are both proven ----

    [Fact]
    public async Task Resource_sorting_honours_field_and_direction()
    {
        await using var db = NewDbContext();
        var service = NewService(db);
        var category = await service.CreateCategoryAsync(new ResourceCategoryRequest("Seeds", null), CancellationToken.None);
        await service.CreateResourceAsync(new ResourceRequest(category.Id, null, "Bravo", "kg", true), CancellationToken.None);
        await service.CreateResourceAsync(new ResourceRequest(category.Id, null, "Alpha", "unit", true), CancellationToken.None);
        await service.CreateResourceAsync(new ResourceRequest(category.Id, null, "Charlie", "bag", true), CancellationToken.None);

        async Task<string[]> Names(string? sortBy, string? direction, int page = 1, int pageSize = 20) =>
            (await service.SearchResourcesAsync(new PagedQuery { SortBy = sortBy, SortDirection = direction, Page = page, PageSize = pageSize }, null, null, CancellationToken.None))
                .Items.Select(item => item.Name).ToArray();

        // Default (no sort supplied) is name ascending.
        Assert.Equal(["Alpha", "Bravo", "Charlie"], await Names(null, null));
        Assert.Equal(["Alpha", "Bravo", "Charlie"], await Names("name", "asc"));
        Assert.Equal(["Charlie", "Bravo", "Alpha"], await Names("name", "desc"));
        Assert.Equal(["Charlie", "Bravo", "Alpha"], await Names(null, "desc"));
        Assert.Equal(["Charlie", "Bravo", "Alpha"], await Names("NAME", "DESC"));
        // Units: bag (Charlie) < kg (Bravo) < unit (Alpha)
        Assert.Equal(["Charlie", "Bravo", "Alpha"], await Names("unit", "asc"));
        Assert.Equal(["Alpha", "Bravo", "Charlie"], await Names("unit", "desc"));
        // Sorting is applied before paging.
        Assert.Equal(["Charlie"], await Names("name", "desc", page: 1, pageSize: 1));
        Assert.Equal(["Alpha"], await Names("name", "desc", page: 3, pageSize: 1));
    }

    [Fact]
    public async Task Category_and_supplier_sorting_honour_direction()
    {
        await using var db = NewDbContext();
        var service = NewService(db);
        foreach (var name in new[] { "Bravo", "Alpha", "Charlie" })
        {
            await service.CreateCategoryAsync(new ResourceCategoryRequest(name, null), CancellationToken.None);
            await service.CreateSupplierAsync(new SupplierRequest(name, $"{name.ToLowerInvariant()}@test.example", "1"), CancellationToken.None);
        }

        var categoriesAsc = await service.SearchCategoriesAsync(new PagedQuery { SortBy = "name", SortDirection = "asc" }, CancellationToken.None);
        var categoriesDesc = await service.SearchCategoriesAsync(new PagedQuery { SortBy = "name", SortDirection = "desc" }, CancellationToken.None);
        var suppliersAsc = await service.SearchSuppliersAsync(new PagedQuery { SortBy = "email", SortDirection = "asc" }, CancellationToken.None);
        var suppliersDesc = await service.SearchSuppliersAsync(new PagedQuery { SortBy = "email", SortDirection = "desc" }, CancellationToken.None);

        Assert.Equal(["Alpha", "Bravo", "Charlie"], categoriesAsc.Items.Select(item => item.Name));
        Assert.Equal(["Charlie", "Bravo", "Alpha"], categoriesDesc.Items.Select(item => item.Name));
        Assert.Equal(["Alpha", "Bravo", "Charlie"], suppliersAsc.Items.Select(item => item.Name));
        Assert.Equal(["Charlie", "Bravo", "Alpha"], suppliersDesc.Items.Select(item => item.Name));
    }

    [Fact]
    public async Task Stock_sorting_and_search_honour_field_direction_and_resource_name()
    {
        await using var db = NewDbContext();
        var service = NewService(db);
        foreach (var (name, quantity) in new[] { ("Bravo", 30m), ("Alpha", 10m), ("Charlie", 20m) })
        {
            await SeedStockAsync(service, name, quantity);
        }

        async Task<string[]> Names(string? sortBy, string? direction, string? search = null) =>
            (await service.SearchStocksAsync(new PagedQuery { SortBy = sortBy, SortDirection = direction, Search = search }, null, CancellationToken.None))
                .Items.Select(item => item.ResourceName).ToArray();

        Assert.Equal(["Alpha", "Bravo", "Charlie"], await Names("resourceName", "asc"));
        Assert.Equal(["Charlie", "Bravo", "Alpha"], await Names("resourceName", "desc"));
        Assert.Equal(["Alpha", "Charlie", "Bravo"], await Names("quantityOnHand", "asc"));
        Assert.Equal(["Bravo", "Charlie", "Alpha"], await Names("quantityOnHand", "desc"));
        Assert.Equal(["Alpha", "Charlie", "Bravo"], await Names("availableQuantity", "asc"));
        Assert.Equal(["Bravo", "Charlie", "Alpha"], await Names("availableQuantity", "desc"));
        Assert.Equal(["Bravo"], await Names("resourceName", "asc", search: "brav"));
        // Default sort (none supplied) still returns every stock.
        Assert.Equal(3, (await Names(null, null)).Length);
        Assert.All((await service.SearchStocksAsync(new PagedQuery(), null, CancellationToken.None)).Items, item => Assert.Equal("kg", item.Unit));
    }

    [Fact]
    public void Paged_query_normalizes_direction_case_and_bounds()
    {
        var query = new PagedQuery { SortBy = "name", SortDirection = "DESC", Page = 0, PageSize = 500 };
        query.Normalize();
        Assert.Equal("desc", query.SortDirection);
        Assert.Equal(1, query.Page);
        Assert.Equal(100, query.PageSize);

        var invalid = new PagedQuery { SortDirection = "sideways" };
        invalid.Normalize();
        Assert.Equal("asc", invalid.SortDirection);
    }

    private static AppDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ResourceService NewService(AppDbContext db) =>
        new(
            db,
            new TestCurrentUserService(),
            new ResourceCategoryRequestValidator(),
            new SupplierRequestValidator(),
            new ResourceRequestValidator(),
            new InventoryStockRequestValidator(),
            new ResourceReservationRequestValidator());

    private static async Task<InventoryStockResponse> SeedStockAsync(ResourceService service, string name, decimal onHand)
    {
        var categories = await service.SearchCategoriesAsync(new PagedQuery(), CancellationToken.None);
        var category = categories.Items.FirstOrDefault() ?? await service.CreateCategoryAsync(new ResourceCategoryRequest("Seeds", null), CancellationToken.None);
        var resource = await service.CreateResourceAsync(new ResourceRequest(category.Id, null, name, "kg", true), CancellationToken.None);
        return await service.UpsertStockAsync(new InventoryStockRequest(resource.Id, onHand, 1), CancellationToken.None);
    }
}
