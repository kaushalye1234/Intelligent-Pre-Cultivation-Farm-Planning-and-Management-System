using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.Resources;

namespace AgriAssist.Api.Services.Resources;

public interface IResourceService
{
    Task<PagedResult<ResourceCategoryResponse>> SearchCategoriesAsync(PagedQuery query, CancellationToken cancellationToken);
    Task<ResourceCategoryResponse> CreateCategoryAsync(ResourceCategoryRequest request, CancellationToken cancellationToken);
    Task<ResourceCategoryResponse> UpdateCategoryAsync(Guid id, ResourceCategoryRequest request, CancellationToken cancellationToken);
    Task DeleteCategoryAsync(Guid id, CancellationToken cancellationToken);
    Task<PagedResult<SupplierResponse>> SearchSuppliersAsync(PagedQuery query, CancellationToken cancellationToken);
    Task<SupplierResponse> CreateSupplierAsync(SupplierRequest request, CancellationToken cancellationToken);
    Task<SupplierResponse> UpdateSupplierAsync(Guid id, SupplierRequest request, CancellationToken cancellationToken);
    Task DeleteSupplierAsync(Guid id, CancellationToken cancellationToken);
    Task<PagedResult<ResourceResponse>> SearchResourcesAsync(PagedQuery query, Guid? categoryId, Guid? supplierId, CancellationToken cancellationToken);
    Task<ResourceResponse> CreateResourceAsync(ResourceRequest request, CancellationToken cancellationToken);
    Task<ResourceResponse> UpdateResourceAsync(Guid id, ResourceRequest request, CancellationToken cancellationToken);
    Task DeleteResourceAsync(Guid id, CancellationToken cancellationToken);
    Task<InventoryStockResponse> UpsertStockAsync(InventoryStockRequest request, CancellationToken cancellationToken);
    Task<PagedResult<InventoryStockResponse>> SearchStocksAsync(PagedQuery query, bool? lowStockOnly, CancellationToken cancellationToken);
    Task<IReadOnlyList<StockTransactionResponse>> GetStockHistoryAsync(Guid stockId, CancellationToken cancellationToken);
    Task<PagedResult<ResourceReservationResponse>> SearchReservationsAsync(PagedQuery query, ResourceReservationStatus? status, CancellationToken cancellationToken);
    Task<ResourceReservationResponse> ReserveAsync(ResourceReservationRequest request, CancellationToken cancellationToken);
    Task<ResourceReservationResponse> StageWorkflowReservationAsync(ResourceReservationRequest request, Guid workflowId, int candidateRevision, Guid requestedByUserId, CancellationToken cancellationToken);
    Task<ResourceReservationResponse> ReleaseAsync(Guid reservationId, CancellationToken cancellationToken);
    Task<ResourceReservationResponse> CancelReservationAsync(Guid reservationId, CancellationToken cancellationToken);
}
