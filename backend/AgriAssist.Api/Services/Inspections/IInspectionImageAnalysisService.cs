using AgriAssist.Api.Dtos.Inspections;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Inspections;

namespace AgriAssist.Api.Services.Inspections;

public interface IInspectionImageAnalysisService
{
    Task<InspectionImageAnalysisStateResponse> AnalyzeAsync(Guid cropPlanRequestId, CancellationToken cancellationToken);
    Task<InspectionImageAnalysisStateResponse> GetCurrentAsync(Guid cropPlanRequestId, CancellationToken cancellationToken);
    Task<InspectionImageAnalysisReviewResponse> ReviewAsync(Guid cropPlanRequestId, InspectionImageAnalysisReviewRequest request, CancellationToken cancellationToken);
    Task<ImageAnalysisCapabilityResponse?> TryGetCapabilityAsync(CancellationToken cancellationToken);
    Task<Guid?> ResolveEligibleReviewIdForSubmissionAsync(FieldInspection inspection, CropPlanRequest planRequest, ImageAnalysisCapabilityResponse? capability, CancellationToken cancellationToken);
    Task<ReviewedImageAnalysisProjection?> GetFrozenProjectionAsync(FieldInspection inspection, CancellationToken cancellationToken);
}
