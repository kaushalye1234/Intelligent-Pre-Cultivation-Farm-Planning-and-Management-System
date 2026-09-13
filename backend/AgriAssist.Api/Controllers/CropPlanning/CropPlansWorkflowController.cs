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

    [HttpPost("{id:guid}/run-field-analysis")]
    [Authorize(Roles = $"{nameof(ApplicationRole.Admin)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.FieldOfficer)}")]
    public async Task<ActionResult<FieldAnalysisRunResponse>> RunFieldAnalysis(Guid id, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.RunFieldAnalysisAsync(id, cancellationToken));

    [HttpGet("{id:guid}/workflow-status")]
    public async Task<ActionResult<CropPlanningWorkflowStatusResponse>> GetWorkflowStatus(Guid id, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.GetWorkflowStatusAsync(id, cancellationToken));

    [HttpGet("{id:guid}/planning-result")]
    public async Task<ActionResult<CropPlanningResultResponse>> GetPlanningResult(Guid id, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.GetPlanningResultAsync(id, cancellationToken));

    [HttpGet("{id:guid}/field-analysis-result")]
    public async Task<ActionResult<FieldAnalysisOutput>> GetFieldAnalysisResult(Guid id, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.GetFieldAnalysisResultAsync(id, cancellationToken));

    [HttpGet("{id:guid}/member-3-handoff")]
    public async Task<ActionResult<Member3HandoffResponse>> GetMember3Handoff(Guid id, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.GetMember3HandoffAsync(id, cancellationToken));
}
