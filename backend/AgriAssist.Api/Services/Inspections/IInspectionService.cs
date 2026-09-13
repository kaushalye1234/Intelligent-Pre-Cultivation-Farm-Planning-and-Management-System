using AgriAssist.Api.Dtos.Inspections;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.Inspections;

namespace AgriAssist.Api.Services.Inspections;

public interface IInspectionService
{
    Task<PagedResult<FieldInspectionResponse>> SearchInspectionsAsync(PagedQuery query, Guid? fieldId, InspectionStatus? status, CancellationToken cancellationToken);
    Task<FieldInspectionDetailResponse> GetInspectionAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<InspectionHistoryEventResponse>> GetInspectionHistoryAsync(Guid id, CancellationToken cancellationToken);
    Task<FieldInspectionResponse> CreateInspectionAsync(FieldInspectionRequest request, CancellationToken cancellationToken);
    Task<FieldInspectionResponse> UpdateInspectionAsync(Guid id, FieldInspectionRequest request, CancellationToken cancellationToken);
    Task<FieldInspectionResponse> SubmitInspectionAsync(Guid id, CancellationToken cancellationToken);
    Task<FieldInspectionResponse> CloseInspectionAsync(Guid id, CancellationToken cancellationToken);
    Task<PagedResult<ObservationResponse>> SearchObservationsAsync(PagedQuery query, Guid? inspectionId, CancellationToken cancellationToken);
    Task<ObservationResponse> CreateObservationAsync(ObservationRequest request, CancellationToken cancellationToken);
    Task<PagedResult<CropIssueResponse>> SearchIssuesAsync(PagedQuery query, CropIssueSeverity? severity, CropIssueStatus? status, CancellationToken cancellationToken);
    Task<CropIssueResponse> GetIssueAsync(Guid id, CancellationToken cancellationToken);
    Task<CropIssueResponse> CreateIssueAsync(CropIssueRequest request, CancellationToken cancellationToken);
    Task<CropIssueResponse> UpdateIssueStatusAsync(Guid issueId, CropIssueStatusRequest request, CancellationToken cancellationToken);
    Task<CropIssueResponse> EscalateIssueAsync(Guid issueId, CancellationToken cancellationToken);
    Task<PagedResult<FollowUpRecommendationResponse>> SearchRecommendationsAsync(PagedQuery query, Guid? cropIssueId, bool? isCompleted, CancellationToken cancellationToken);
    Task<FollowUpRecommendationResponse> CreateRecommendationAsync(FollowUpRecommendationRequest request, CancellationToken cancellationToken);
    Task<FollowUpRecommendationResponse> UpdateRecommendationAsync(Guid id, FollowUpRecommendationUpdateRequest request, CancellationToken cancellationToken);
    Task<InspectionImageResponse> UploadImageAsync(Guid inspectionId, IFormFile file, CancellationToken cancellationToken);
    Task<IReadOnlyList<InspectionImageResponse>> GetInspectionImagesAsync(Guid inspectionId, CancellationToken cancellationToken);
}
