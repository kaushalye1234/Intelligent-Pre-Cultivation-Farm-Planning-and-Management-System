using System.Net;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.ExternalServices.Weather;
using AgriAssist.Api.Models.Resources;
using AgriAssist.Api.Services.Shared;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Services.Resources;

/// <summary>Read-only evidence tools for the WeatherResourceAgent. Nothing here changes inventory.</summary>
public interface IWeatherResourceToolService
{
    Task<IReadOnlyList<StockSnapshot>> GetResourceAvailabilityAsync(IReadOnlyCollection<Guid> resourceIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReservationSnapshot>> GetExistingReservationsAsync(IReadOnlyCollection<Guid> resourceIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<StockSnapshot>> GetLowStockStatusAsync(CancellationToken cancellationToken);
    Task<WeatherForecastResponse> GetWeatherForecastAsync(Guid workflowId, CancellationToken cancellationToken);
}

public sealed class WeatherResourceToolService(AppDbContext dbContext, IWeatherService weatherService) : IWeatherResourceToolService
{
    public const int MaxRows = 100;
    public const int MaxRequestedResources = 50;

    /// <summary>
    /// Stocks for the requested resources are always included; the rest of the inventory fills the list up to
    /// <see cref="MaxRows"/> rows. AvailableQuantity is QuantityOnHand - ReservedQuantity, as everywhere else.
    /// </summary>
    public async Task<IReadOnlyList<StockSnapshot>> GetResourceAvailabilityAsync(IReadOnlyCollection<Guid> resourceIds, CancellationToken cancellationToken)
    {
        var ids = resourceIds.Distinct().ToArray();
        List<StockSnapshot> requested = ids.Length == 0
            ? []
            : await Snapshot(ActiveStocks().Where(stock => ids.Contains(stock.ResourceId))).ToListAsync(cancellationToken);
        var others = await Snapshot(ActiveStocks().Where(stock => !ids.Contains(stock.ResourceId))
                .OrderBy(stock => stock.Resource!.Name).ThenBy(stock => stock.Id)
                .Take(Math.Max(0, MaxRows - requested.Count)))
            .ToListAsync(cancellationToken);
        return requested.OrderBy(stock => stock.ResourceName).Concat(others).ToList();
    }

    public async Task<IReadOnlyList<ReservationSnapshot>> GetExistingReservationsAsync(IReadOnlyCollection<Guid> resourceIds, CancellationToken cancellationToken)
    {
        var ids = resourceIds.Distinct().ToArray();
        var reservations = dbContext.ResourceReservations.AsNoTracking()
            .Where(reservation => !reservation.IsDeleted && reservation.Status == ResourceReservationStatus.Active
                && reservation.InventoryStock != null && !reservation.InventoryStock.IsDeleted && reservation.InventoryStock.Resource != null);
        if (ids.Length > 0) reservations = reservations.Where(reservation => ids.Contains(reservation.InventoryStock!.ResourceId));

        return await reservations
            .OrderByDescending(reservation => reservation.CreatedAt).ThenBy(reservation => reservation.Id)
            .Take(MaxRows * 2)
            .Select(reservation => new ReservationSnapshot(
                reservation.Id,
                reservation.InventoryStockId,
                reservation.InventoryStock!.ResourceId,
                reservation.InventoryStock.Resource!.Name,
                reservation.InventoryStock.Resource.Unit,
                reservation.Quantity,
                reservation.Purpose,
                reservation.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StockSnapshot>> GetLowStockStatusAsync(CancellationToken cancellationToken) =>
        await Snapshot(ActiveStocks()
                .Where(stock => stock.QuantityOnHand - stock.ReservedQuantity <= stock.LowStockThreshold)
                .OrderBy(stock => stock.Resource!.Name).ThenBy(stock => stock.Id)
                .Take(MaxRows))
            .ToListAsync(cancellationToken);

    /// <summary>Forecast for the farm location of the workflow's crop plan. Provider failures come back unavailable.</summary>
    public async Task<WeatherForecastResponse> GetWeatherForecastAsync(Guid workflowId, CancellationToken cancellationToken)
    {
        var farmLocation = await dbContext.AgentWorkflows.AsNoTracking()
            .Where(workflow => workflow.Id == workflowId && !workflow.IsDeleted && workflow.CropPlanRequest != null)
            .Select(workflow => new
            {
                workflow.CropPlanRequest!.Farm!.Location,
                workflow.CropPlanRequest.Farm.District
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(HttpStatusCode.NotFound, "NOT_FOUND", "Workflow crop plan was not found.");
        var location = WeatherLocationResolver.Resolve(farmLocation.Location, farmLocation.District);
        return await weatherService.GetForecastAsync(location, cancellationToken);
    }

    private IQueryable<InventoryStock> ActiveStocks() =>
        dbContext.InventoryStocks.AsNoTracking()
            .Where(stock => !stock.IsDeleted && stock.Resource != null && stock.Resource.IsActive && !stock.Resource.IsDeleted);

    private static IQueryable<StockSnapshot> Snapshot(IQueryable<InventoryStock> stocks) =>
        stocks.Select(stock => new StockSnapshot(
            stock.Id,
            stock.ResourceId,
            stock.Resource!.Name,
            stock.Resource.Unit,
            stock.QuantityOnHand,
            stock.ReservedQuantity,
            stock.QuantityOnHand - stock.ReservedQuantity,
            stock.LowStockThreshold));
}
