using AgriAssist.Api.Dtos.Inspections;

namespace AgriAssist.Api.ExternalServices.AgenticAI;

public interface IInspectionImageAnalysisAIClient
{
    Task<ImageAnalysisCapabilityResponse> GetImageAnalysisCapabilityAsync(CancellationToken cancellationToken);
    Task<InspectionImageAnalysisAiResponse> RunImageAnalysisAsync(InspectionImageAnalysisAiInput input, CancellationToken cancellationToken);
}
