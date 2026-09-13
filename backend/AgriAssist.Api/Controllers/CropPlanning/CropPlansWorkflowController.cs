using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.CropPlanning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriAssist.Api.Controllers.CropPlanning;

[ApiController]
[Route("api/crop-plans")]
[Authorize]
public sealed class CropPlansWorkflowController(ICropPlanningService cropPlanningService) : ControllerBase
{
    [HttpPost("{id:guid}/start-ai-workflow")]
    [Authorize(Roles = $"{nameof(ApplicationRole.Farmer)},{nameof(ApplicationRole.Admin)},{nameof(ApplicationRole.AgriculturalOfficer)}")]
    public async Task<ActionResult<CropPlanningWorkflowStartResponse>> StartAiWorkflow(Guid id, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.StartAiWorkflowAsync(id, cancellationToken));

    [HttpGet("{id:guid}/workflow-status")]
    public async Task<ActionResult<CropPlanningWorkflowStatusResponse>> GetWorkflowStatus(Guid id, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.GetWorkflowStatusAsync(id, cancellationToken));

    [HttpGet("{id:guid}/planning-result")]
    public async Task<ActionResult<CropPlanningResultResponse>> GetPlanningResult(Guid id, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.GetPlanningResultAsync(id, cancellationToken));
}
