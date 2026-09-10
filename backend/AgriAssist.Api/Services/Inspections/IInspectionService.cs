using AgriAssist.Api.Dtos.Inspections;
using AgriAssist.Api.Dtos.Shared;

namespace AgriAssist.Api.Services.Inspections;

public interface IInspectionService
{
    Task<PagedResult<FieldInspectionResponse>> SearchInspectionsAsync(PagedQuery query, Guid? fieldId, CancellationToken cancellationToken);
    Task<FieldInspectionResponse> CreateInspectionAsync(FieldInspectionRequest request, CancellationToken cancellationToken);
    Task<FieldInspectionResponse> UpdateInspectionAsync(Guid id, FieldInspectionRequest request, CancellationToken cancellationToken);
    Task<PagedResult<ObservationResponse>> SearchObservationsAsync(PagedQuery query, Guid? inspectionId, CancellationToken cancellationToken);
    Task<ObservationResponse> CreateObservationAsync(ObservationRequest request, CancellationToken cancellationToken);
    Task<PagedResult<CropIssueResponse>> SearchIssuesAsync(PagedQuery query, CancellationToken cancellationToken);
    Task<CropIssueResponse> CreateIssueAsync(CropIssueRequest request, CancellationToken cancellationToken);
    Task<CropIssueResponse> EscalateIssueAsync(Guid issueId, CancellationToken cancellationToken);
    Task<FollowUpRecommendationResponse> CreateRecommendationAsync(FollowUpRecommendationRequest request, CancellationToken cancellationToken);
    Task<InspectionImageResponse> UploadImageAsync(Guid inspectionId, IFormFile file, CancellationToken cancellationToken);
}
