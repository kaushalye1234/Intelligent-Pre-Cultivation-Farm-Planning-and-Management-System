using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.Resources;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriAssist.Api.Controllers.Resources;

[ApiController]
[Route("api/resources")]
[Authorize]
public sealed class ResourcesController(IResourceService resourceService) : ControllerBase
{
    [HttpGet("categories")]
    public async Task<ActionResult<PagedResult<ResourceCategoryResponse>>> Categories([FromQuery] PagedQuery query, CancellationToken cancellationToken) =>
        Ok(await resourceService.SearchCategoriesAsync(query, cancellationToken));

    [HttpPost("categories")]
    [Authorize(Roles = $"{nameof(ApplicationRole.ResourceOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<ResourceCategoryResponse>> CreateCategory(ResourceCategoryRequest request, CancellationToken cancellationToken) =>
        Ok(await resourceService.CreateCategoryAsync(request, cancellationToken));

    [HttpPut("categories/{id:guid}")]
    [Authorize(Roles = $"{nameof(ApplicationRole.ResourceOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<ResourceCategoryResponse>> UpdateCategory(Guid id, ResourceCategoryRequest request, CancellationToken cancellationToken) =>
        Ok(await resourceService.UpdateCategoryAsync(id, request, cancellationToken));

    [HttpDelete("categories/{id:guid}")]
    [Authorize(Roles = $"{nameof(ApplicationRole.ResourceOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken cancellationToken)
    {
        await resourceService.DeleteCategoryAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("suppliers")]
    public async Task<ActionResult<PagedResult<SupplierResponse>>> Suppliers([FromQuery] PagedQuery query, CancellationToken cancellationToken) =>
        Ok(await resourceService.SearchSuppliersAsync(query, cancellationToken));

    [HttpPost("suppliers")]
    [Authorize(Roles = $"{nameof(ApplicationRole.ResourceOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<SupplierResponse>> CreateSupplier(SupplierRequest request, CancellationToken cancellationToken) =>
        Ok(await resourceService.CreateSupplierAsync(request, cancellationToken));

    [HttpPut("suppliers/{id:guid}")]
    [Authorize(Roles = $"{nameof(ApplicationRole.ResourceOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<SupplierResponse>> UpdateSupplier(Guid id, SupplierRequest request, CancellationToken cancellationToken) =>
        Ok(await resourceService.UpdateSupplierAsync(id, request, cancellationToken));

    [HttpDelete("suppliers/{id:guid}")]
    [Authorize(Roles = $"{nameof(ApplicationRole.ResourceOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<IActionResult> DeleteSupplier(Guid id, CancellationToken cancellationToken)
    {
        await resourceService.DeleteSupplierAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ResourceResponse>>> Search([FromQuery] PagedQuery query, [FromQuery] Guid? categoryId, [FromQuery] Guid? supplierId, CancellationToken cancellationToken) =>
        Ok(await resourceService.SearchResourcesAsync(query, categoryId, supplierId, cancellationToken));

    [HttpPost]
    [Authorize(Roles = $"{nameof(ApplicationRole.ResourceOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<ResourceResponse>> Create(ResourceRequest request, CancellationToken cancellationToken) =>
        Ok(await resourceService.CreateResourceAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{nameof(ApplicationRole.ResourceOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<ResourceResponse>> Update(Guid id, ResourceRequest request, CancellationToken cancellationToken) =>
        Ok(await resourceService.UpdateResourceAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = $"{nameof(ApplicationRole.ResourceOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await resourceService.DeleteResourceAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("stocks")]
    public async Task<ActionResult<PagedResult<InventoryStockResponse>>> Stocks([FromQuery] PagedQuery query, [FromQuery] bool? lowStockOnly, CancellationToken cancellationToken) =>
        Ok(await resourceService.SearchStocksAsync(query, lowStockOnly, cancellationToken));

    [HttpPost("stocks")]
    [Authorize(Roles = $"{nameof(ApplicationRole.ResourceOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<InventoryStockResponse>> UpsertStock(InventoryStockRequest request, CancellationToken cancellationToken) =>
        Ok(await resourceService.UpsertStockAsync(request, cancellationToken));

    [HttpGet("stocks/{id:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<StockTransactionResponse>>> History(Guid id, CancellationToken cancellationToken) =>
        Ok(await resourceService.GetStockHistoryAsync(id, cancellationToken));

    [HttpGet("reservations")]
    public async Task<ActionResult<PagedResult<ResourceReservationResponse>>> Reservations([FromQuery] PagedQuery query, [FromQuery] ResourceReservationStatus? status, CancellationToken cancellationToken) =>
        Ok(await resourceService.SearchReservationsAsync(query, status, cancellationToken));

    [HttpPost("reservations")]
    public async Task<ActionResult<ResourceReservationResponse>> Reserve(ResourceReservationRequest request, CancellationToken cancellationToken) =>
        Ok(await resourceService.ReserveAsync(request, cancellationToken));

    [HttpPost("reservations/{id:guid}/release")]
    public async Task<ActionResult<ResourceReservationResponse>> Release(Guid id, CancellationToken cancellationToken) =>
        Ok(await resourceService.ReleaseAsync(id, cancellationToken));

    [HttpPost("reservations/{id:guid}/cancel")]
    public async Task<ActionResult<ResourceReservationResponse>> Cancel(Guid id, CancellationToken cancellationToken) =>
        Ok(await resourceService.CancelReservationAsync(id, cancellationToken));
}
