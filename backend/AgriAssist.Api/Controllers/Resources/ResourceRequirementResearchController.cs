using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriAssist.Api.Controllers.Resources;

/// <summary>
/// Research can return an unverified, sourced draft to Agricultural Officers and Admins. Only Admins
/// can save the recommendation as a verified ResourceRequirement rule for the WeatherResourceAgent.
/// </summary>
[ApiController]
[Route("api/resources/requirement-research")]
[Authorize]
public sealed class ResourceRequirementResearchController(IResourceRequirementResearchService researchService) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = $"{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<ResourceRequirementResearchResponse>> Research(
        ResourceRequirementResearchRequest request,
        CancellationToken cancellationToken) =>
        Ok(await researchService.ResearchAsync(request, cancellationToken));

    [HttpPost("verify")]
    [Authorize(Roles = nameof(ApplicationRole.Admin))]
    public async Task<ActionResult<VerifiedResourceRequirementResponse>> Verify(
        VerifyResourceRequirementRequest request,
        CancellationToken cancellationToken) =>
        Ok(await researchService.VerifyAndSaveAsync(request, cancellationToken));
}
