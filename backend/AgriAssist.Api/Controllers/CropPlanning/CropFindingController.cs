using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.CropPlanning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriAssist.Api.Controllers.CropPlanning;

[ApiController]
[Route("api/crop-finding")]
[Authorize(Roles = nameof(ApplicationRole.Admin))]
public sealed class CropFindingController(ICropFindingService cropFindingService) : ControllerBase
{
    [HttpPost("suggest-crops")]
    public async Task<ActionResult<CropSuggestionsResponse>> SuggestCrops(
        SuggestCropsRequest request,
        CancellationToken cancellationToken) =>
        Ok(await cropFindingService.SuggestCropsAsync(request, cancellationToken));

    [HttpPost("suggest-varieties")]
    public async Task<ActionResult<VarietySuggestionsResponse>> SuggestVarieties(
        SuggestVarietiesRequest request,
        CancellationToken cancellationToken) =>
        Ok(await cropFindingService.SuggestVarietiesAsync(request, cancellationToken));

    [HttpPost("discover-references")]
    public async Task<ActionResult<ReferenceDiscoveryResponse>> DiscoverReferences(
        DiscoverReferencesRequest request,
        CancellationToken cancellationToken) =>
        Ok(await cropFindingService.DiscoverReferencesAsync(request, cancellationToken));
}
