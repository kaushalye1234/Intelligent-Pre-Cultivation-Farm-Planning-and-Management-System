using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Models.Resources;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.Resources;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.Resources;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Tests;

/// <summary>
/// Real PostgreSQL coverage for inventory concurrency. The EF InMemory tests in ResourceBusinessRuleTests
/// cannot prove row-level behaviour, so these run against a database named by
/// AGRIASSIST_TEST_POSTGRES_CONNECTION_STRING (the same variable the FarmCapacity tests use) and are skipped without it.
/// </summary>
[Collection(PostgreSqlCollection.Name)]
public sealed class ResourceInventoryPostgreSqlIntegrationTests
{
    private const string ConnectionVariable = "AGRIASSIST_TEST_POSTGRES_CONNECTION_STRING";
    private const string Ok = "OK";

    [PostgreSqlFact]
    public async Task Concurrent_reservations_never_oversell_and_never_lose_an_update()
    {
        var connectionString = RequiredConnectionString();
        var data = await SeedAsync(connectionString, onHand: 10m);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // 16 users each try to reserve 1 unit of a 10 unit stock. Callers retry when they lose an
        // optimistic-concurrency race (STOCK_CHANGED), exactly as a client refreshing and resubmitting would.
        var attempts = Enumerable.Range(1, 16).Select(async index =>
        {
            await gate.Task;
            return await AttemptAsync(connectionString, data.UserId, service =>
                service.ReserveAsync(new ResourceReservationRequest(data.StockId, 1m, $"Concurrent {index}"), CancellationToken.None));
        }).ToArray();
        gate.SetResult();
        var outcomes = await Task.WhenAll(attempts);

        await using var verification = NewDbContext(connectionString);
        var stock = await verification.InventoryStocks.AsNoTracking().SingleAsync(item => item.Id == data.StockId);
        var activeReserved = await verification.ResourceReservations
            .Where(item => item.InventoryStockId == data.StockId && item.Status == ResourceReservationStatus.Active)
            .SumAsync(item => item.Quantity);
        var reserveLedger = await verification.StockTransactions
            .Where(item => item.InventoryStockId == data.StockId && item.Type == StockTransactionType.Reserve)
            .SumAsync(item => item.Quantity);

        Assert.Equal(10, outcomes.Count(outcome => outcome == Ok));
        Assert.Equal(6, outcomes.Count(outcome => outcome == "INSUFFICIENT_STOCK"));
        Assert.Equal(16, outcomes.Count(outcome => outcome is Ok or "INSUFFICIENT_STOCK"));
        Assert.Equal(10m, stock.ReservedQuantity);
        Assert.Equal(0m, stock.AvailableQuantity);
        Assert.True(stock.AvailableQuantity >= 0);
        Assert.Equal(stock.ReservedQuantity, activeReserved);
        Assert.Equal(stock.ReservedQuantity, reserveLedger);
    }

    [PostgreSqlFact]
    public async Task Concurrent_reserve_and_stock_reduction_cannot_leave_stock_below_reserved()
    {
        var connectionString = RequiredConnectionString();
        var data = await SeedAsync(connectionString, onHand: 10m);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Reserving 8 and lowering on-hand to 5 cannot both succeed, whichever order they commit in.
        var reserve = Task.Run(async () =>
        {
            await gate.Task;
            return await AttemptAsync(connectionString, data.UserId, service =>
                service.ReserveAsync(new ResourceReservationRequest(data.StockId, 8m, "Planting"), CancellationToken.None));
        });
        var reduce = Task.Run(async () =>
        {
            await gate.Task;
            return await AttemptAsync(connectionString, data.UserId, service =>
                service.UpsertStockAsync(new InventoryStockRequest(data.ResourceId, 5m, 1m), CancellationToken.None));
        });
        gate.SetResult();
        var outcomes = await Task.WhenAll(reserve, reduce);

        await using var verification = NewDbContext(connectionString);
        var stock = await verification.InventoryStocks.AsNoTracking().SingleAsync(item => item.Id == data.StockId);
        Assert.Equal(1, outcomes.Count(outcome => outcome == Ok));
        Assert.Contains(outcomes, outcome => outcome is "INSUFFICIENT_STOCK" or "STOCK_BELOW_RESERVED");
        Assert.True(stock.ReservedQuantity <= stock.QuantityOnHand);
        Assert.True(stock.AvailableQuantity >= 0);
    }

    [PostgreSqlFact]
    public async Task Concurrent_releases_of_one_reservation_succeed_once_and_never_go_negative()
    {
        var connectionString = RequiredConnectionString();
        var data = await SeedAsync(connectionString, onHand: 10m);
        await using (var setup = NewDbContext(connectionString))
        {
            await NewService(setup, data.UserId).ReserveAsync(new ResourceReservationRequest(data.StockId, 4m, "Planting"), CancellationToken.None);
        }

        Guid reservationId;
        await using (var lookup = NewDbContext(connectionString))
        {
            reservationId = await lookup.ResourceReservations.Where(item => item.InventoryStockId == data.StockId).Select(item => item.Id).SingleAsync();
        }

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = Enumerable.Range(1, 6).Select(async index =>
        {
            await gate.Task;
            return await AttemptAsync(connectionString, data.UserId, service => service.ReleaseAsync(reservationId, CancellationToken.None));
        }).ToArray();
        gate.SetResult();
        var outcomes = await Task.WhenAll(attempts);

        await using var verification = NewDbContext(connectionString);
        var stock = await verification.InventoryStocks.AsNoTracking().SingleAsync(item => item.Id == data.StockId);
        var releaseEntries = await verification.StockTransactions.CountAsync(item => item.InventoryStockId == data.StockId && item.Type == StockTransactionType.Release);
        Assert.Equal(1, outcomes.Count(outcome => outcome == Ok));
        Assert.Equal(5, outcomes.Count(outcome => outcome == "RESERVATION_NOT_ACTIVE"));
        Assert.Equal(0m, stock.ReservedQuantity);
        Assert.Equal(10m, stock.AvailableQuantity);
        Assert.Equal(1, releaseEntries);
    }

    [PostgreSqlFact]
    public async Task Stale_stock_write_is_rejected_by_the_row_version_instead_of_overwriting()
    {
        var connectionString = RequiredConnectionString();
        var data = await SeedAsync(connectionString, onHand: 10m);

        // Two users load the same stock row, then both try to reserve from it.
        await using var firstDb = NewDbContext(connectionString);
        await using var secondDb = NewDbContext(connectionString);
        _ = await firstDb.InventoryStocks.SingleAsync(item => item.Id == data.StockId);
        _ = await secondDb.InventoryStocks.SingleAsync(item => item.Id == data.StockId);

        await NewService(firstDb, data.UserId).ReserveAsync(new ResourceReservationRequest(data.StockId, 6m, "First"), CancellationToken.None);
        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            NewService(secondDb, data.UserId).ReserveAsync(new ResourceReservationRequest(data.StockId, 6m, "Second"), CancellationToken.None));

        Assert.Equal("STOCK_CHANGED", exception.Code);
        await using var verification = NewDbContext(connectionString);
        Assert.Equal(6m, (await verification.InventoryStocks.AsNoTracking().SingleAsync(item => item.Id == data.StockId)).ReservedQuantity);
        Assert.Equal(1, await verification.ResourceReservations.CountAsync(item => item.InventoryStockId == data.StockId));
    }

    private static async Task<string> AttemptAsync(string connectionString, Guid userId, Func<ResourceService, Task> action)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await using var dbContext = NewDbContext(connectionString);
            try
            {
                await action(NewService(dbContext, userId));
                return Ok;
            }
            catch (ApiException exception) when (exception.Code == "STOCK_CHANGED")
            {
                await Task.Delay(Random.Shared.Next(1, 15));
            }
            catch (ApiException exception)
            {
                return exception.Code;
            }
        }

        return "RETRIES_EXHAUSTED";
    }

    private static string RequiredConnectionString() =>
        Environment.GetEnvironmentVariable(ConnectionVariable)
        ?? throw new InvalidOperationException($"{ConnectionVariable} is required.");

    private static AppDbContext NewDbContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);

    private static ResourceService NewService(AppDbContext dbContext, Guid userId) =>
        new(
            dbContext,
            new OfficerCurrentUser(userId),
            new ResourceCategoryRequestValidator(),
            new SupplierRequestValidator(),
            new ResourceRequestValidator(),
            new InventoryStockRequestValidator(),
            new ResourceReservationRequestValidator());

    private static async Task<(Guid UserId, Guid ResourceId, Guid StockId)> SeedAsync(string connectionString, decimal onHand)
    {
        await using var dbContext = NewDbContext(connectionString);
        await dbContext.Database.MigrateAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var officer = new AppUser
        {
            FullName = "PostgreSQL Resource Officer",
            Email = $"postgres-resources-{suffix}@example.test",
            PasswordHash = "hash",
            Role = ApplicationRole.ResourceOfficer,
            IsActive = true
        };
        var category = new ResourceCategory { Name = $"Concurrency {suffix}" };
        var resource = new Resource { Name = $"Concurrent Seed {suffix}", Unit = "kg", ResourceCategory = category };
        var stock = new InventoryStock { Resource = resource, QuantityOnHand = onHand, ReservedQuantity = 0, LowStockThreshold = 1, RowVersion = Guid.NewGuid().ToByteArray() };
        dbContext.AddRange(officer, category, resource, stock);
        await dbContext.SaveChangesAsync();
        return (officer.Id, resource.Id, stock.Id);
    }

    private sealed class OfficerCurrentUser(Guid userId) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;
        public ApplicationRole? Role => ApplicationRole.ResourceOfficer;
        public bool IsInRole(ApplicationRole role) => role == ApplicationRole.ResourceOfficer;
    }

    private sealed class PostgreSqlFactAttribute : FactAttribute
    {
        public PostgreSqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
            {
                Skip = $"Set {ConnectionVariable} to run the PostgreSQL inventory concurrency tests.";
            }
        }
    }
}
