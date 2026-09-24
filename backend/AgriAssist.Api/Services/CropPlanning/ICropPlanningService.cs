using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Dtos.Shared;

namespace AgriAssist.Api.Services.CropPlanning;

public interface ICropPlanningService
{
    Task<FarmerOnboardingStatusResponse> GetFarmerOnboardingStatusAsync(CancellationToken cancellationToken);

    Task<PagedResult<FarmResponse>> SearchFarmsAsync(PagedQuery query, CancellationToken cancellationToken);
    Task<FarmResponse> GetFarmAsync(Guid id, CancellationToken cancellationToken);
    Task<FarmResponse> CreateFarmAsync(FarmRequest request, CancellationToken cancellationToken);
    Task<FarmResponse> UpdateFarmAsync(Guid id, FarmRequest request, CancellationToken cancellationToken);
    Task DeleteFarmAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<FieldResponse>> SearchFieldsAsync(PagedQuery query, Guid? farmId, CancellationToken cancellationToken);
    Task<FieldResponse> CreateFieldAsync(FieldRequest request, CancellationToken cancellationToken);
    Task<FieldResponse> UpdateFieldAsync(Guid id, FieldRequest request, CancellationToken cancellationToken);

    Task<PagedResult<CropTypeResponse>> SearchCropTypesAsync(PagedQuery query, CancellationToken cancellationToken, bool includeInactive = false);
    Task<CropTypeResponse> CreateCropTypeAsync(CropTypeRequest request, CancellationToken cancellationToken);
    Task<CropTypeResponse> UpdateCropTypeAsync(Guid id, CropTypeRequest request, CancellationToken cancellationToken);
    Task<PagedResult<CropVarietyResponse>> SearchCropVarietiesAsync(PagedQuery query, Guid? cropTypeId, CancellationToken cancellationToken, bool includeInactive = false);
    Task<CropVarietyResponse> CreateCropVarietyAsync(CropVarietyRequest request, CancellationToken cancellationToken);
    Task<CropVarietyResponse> UpdateCropVarietyAsync(Guid id, CropVarietyRequest request, CancellationToken cancellationToken);
    Task<PagedResult<CropReferenceProfileResponse>> SearchReferenceProfilesAsync(PagedQuery query, Guid? cropTypeId, CancellationToken cancellationToken);
    Task<CropReferenceProfileResponse> CreateReferenceProfileAsync(CropReferenceProfileRequest request, CancellationToken cancellationToken);
    Task<CropReferenceProfileResponse> SetReferenceProfileActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken);

    Task<PagedResult<CropCycleResponse>> SearchCropCyclesAsync(PagedQuery query, Guid? fieldId, CancellationToken cancellationToken);
    Task<CropCycleResponse> CreateCropCycleAsync(CropCycleRequest request, CancellationToken cancellationToken);

    Task<PagedResult<CropPlanRequestResponse>> SearchCropPlanRequestsAsync(PagedQuery query, CancellationToken cancellationToken);
    Task<CropPlanRequestResponse> CreateCropPlanRequestAsync(CropPlanRequestCreate request, CancellationToken cancellationToken);
    Task<CropPlanRequestResponse> UpdateCropPlanRequestAsync(Guid id, CropPlanRequestUpdate request, CancellationToken cancellationToken);
    Task<CropPlanRequestResponse> GeneratePreliminaryRequestAsync(CropPlanRequestCreate request, CancellationToken cancellationToken);
    Task<IReadOnlyList<CropPlanHistoryResponse>> GetCropPlanHistoryAsync(Guid requestId, CancellationToken cancellationToken);
    Task<CropPlanningWorkflowStartResponse> StartAiWorkflowAsync(Guid requestId, CancellationToken cancellationToken);
    Task<PrePlantingAssessmentResponse?> GetPrePlantingAssessmentAsync(Guid requestId, CancellationToken cancellationToken);
    Task<PrePlantingAssessmentResponse> SavePrePlantingAssessmentAsync(Guid requestId, PrePlantingAssessmentRequest request, CancellationToken cancellationToken);
    Task<FieldAnalysisRunResponse> RunFieldAnalysisAsync(Guid requestId, CancellationToken cancellationToken);
    Task<CropPlanningWorkflowStatusResponse> GetWorkflowStatusAsync(Guid requestId, CancellationToken cancellationToken);
    Task<CropPlanningResultResponse> GetPlanningResultAsync(Guid requestId, CancellationToken cancellationToken);
    Task<FieldAnalysisOutput> GetFieldAnalysisResultAsync(Guid requestId, CancellationToken cancellationToken);
    Task<Member3HandoffResponse> GetMember3HandoffAsync(Guid requestId, CancellationToken cancellationToken);
}
