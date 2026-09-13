using AgriAssist.Api.Dtos.CropPlanning;

namespace AgriAssist.Api.ExternalServices.AgenticAI;

public interface IAgenticAIClient
{
    Task<CropPlanningCoordinatorOutput> RunCropPlanningCoordinatorAsync(CropPlanningCoordinatorInput input, CancellationToken cancellationToken);
}
