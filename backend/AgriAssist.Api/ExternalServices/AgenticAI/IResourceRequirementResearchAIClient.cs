using AgriAssist.Api.Dtos.Resources;

namespace AgriAssist.Api.ExternalServices.AgenticAI;

/// <summary>
/// Admin-only Member 3 research call. Uses the CropFinding transport (web search budget and error envelope);
/// it is separate from the 45-second WeatherResourceAgent workflow client.
/// </summary>
public interface IResourceRequirementResearchAIClient
{
    Task<ResourceRequirementResearchResponse> ResearchResourceRequirementAsync(
        ResourceRequirementResearchInput input,
        CancellationToken cancellationToken);
}
