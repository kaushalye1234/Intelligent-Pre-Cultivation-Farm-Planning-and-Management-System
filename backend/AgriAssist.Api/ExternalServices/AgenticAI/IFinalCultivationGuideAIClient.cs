using AgriAssist.Api.Dtos.FinalCultivationGuide;

namespace AgriAssist.Api.ExternalServices.AgenticAI;

public interface IFinalCultivationGuideAIClient
{
    Task<FinalCultivationGuideOutputDto> GenerateFinalCultivationGuideAsync(
        FinalCultivationGuideInputDto input,
        CancellationToken cancellationToken);
}
