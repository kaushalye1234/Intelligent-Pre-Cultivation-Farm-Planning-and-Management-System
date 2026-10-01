using AgriAssist.Api.Dtos.CropPlanning;

namespace AgriAssist.Api.ExternalServices.AgenticAI;

/// <summary>Admin-only Member 1 discovery calls. Kept separate from the runtime workflow client.</summary>
public interface ICropFindingAIClient
{
    Task<CropSuggestionsResponse> SuggestCropsAsync(SuggestCropsInput input, CancellationToken cancellationToken);
    Task<VarietySuggestionsResponse> SuggestVarietiesAsync(SuggestVarietiesInput input, CancellationToken cancellationToken);
    Task<ReferenceDiscoveryResponse> DiscoverReferencesAsync(DiscoverReferencesInput input, CancellationToken cancellationToken);
}
