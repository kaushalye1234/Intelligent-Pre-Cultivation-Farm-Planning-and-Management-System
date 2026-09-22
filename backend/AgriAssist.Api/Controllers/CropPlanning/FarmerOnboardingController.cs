using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.CropPlanning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriAssist.Api.Controllers.CropPlanning;

[ApiController]
[Route("api/farmer/onboarding-status")]
[Authorize(Roles = nameof(ApplicationRole.Farmer))]
public sealed class FarmerOnboardingController(ICropPlanningService cropPlanningService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<FarmerOnboardingStatusResponse>> GetStatus(CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.GetFarmerOnboardingStatusAsync(cancellationToken));
}
