using AgriAssist.Api.Dtos.CropPlanning;

namespace AgriAssist.Api.Services.CropPlanning;

public interface ICropFindingService
{
    Task<CropSuggestionsResponse> SuggestCropsAsync(SuggestCropsRequest request, CancellationToken cancellationToken);
    Task<VarietySuggestionsResponse> SuggestVarietiesAsync(SuggestVarietiesRequest request, CancellationToken cancellationToken);
    Task<ReferenceDiscoveryResponse> DiscoverReferencesAsync(DiscoverReferencesRequest request, CancellationToken cancellationToken);
}
