using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriAssist.Api.Controllers.Resources;

/// <summary>
/// Member 3 Admin-only Resource Requirement Research. Research returns an unverified, sourced draft and never saves;
/// saving creates an inactive draft for Agricultural Officer review before agent use.
/// </summary>
[ApiController]
[Route("api/resources/requirement-research")]
[Authorize(Roles = nameof(ApplicationRole.Admin))]
public sealed class ResourceRequirementResearchController(IResourceRequirementResearchService researchService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ResourceRequirementResearchResponse>> Research(
        ResourceRequirementResearchRequest request,
        CancellationToken cancellationToken) =>
        Ok(await researchService.ResearchAsync(request, cancellationToken));

    [HttpPost("draft")]
    [HttpPost("verify")] // Compatibility alias; this endpoint creates an inactive draft.

    public async Task<ActionResult<VerifiedResourceRequirementResponse>> Verify(
        VerifyResourceRequirementRequest request,
        CancellationToken cancellationToken) =>
        Ok(await researchService.VerifyAndSaveAsync(request, cancellationToken));
}
