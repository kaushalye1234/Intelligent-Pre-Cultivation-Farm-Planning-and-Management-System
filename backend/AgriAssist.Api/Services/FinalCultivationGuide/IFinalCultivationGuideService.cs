using AgriAssist.Api.Dtos.FinalCultivationGuide;

namespace AgriAssist.Api.Services.FinalCultivationGuide;

public interface IFinalCultivationGuideService
{
    Task<FinalCultivationGuideStatusDto> GenerateAsync(Guid workflowId, int approvedRevision, CancellationToken cancellationToken);
}
