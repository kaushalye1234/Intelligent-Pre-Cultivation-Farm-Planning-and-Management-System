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

    [HttpGet("{id:guid}/pre-planting-assessment")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<PrePlantingAssessmentResponse?>> GetPrePlantingAssessment(Guid id, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.GetPrePlantingAssessmentAsync(id, cancellationToken));

    [HttpGet("{id:guid}/pre-planting-context")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<PrePlantingContextResponse>> GetPrePlantingContext(Guid id, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.GetPrePlantingContextAsync(id, cancellationToken));

    [HttpPut("{id:guid}/pre-planting-assessment")]
    [Authorize(Roles = nameof(ApplicationRole.FieldOfficer))]
    public async Task<ActionResult<PrePlantingAssessmentResponse>> SavePrePlantingAssessment(
        Guid id,
        PrePlantingAssessmentRequest request,
        CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.SavePrePlantingAssessmentAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/pre-planting-assessment/submit")]
    [Authorize(Roles = nameof(ApplicationRole.FieldOfficer))]
    public async Task<ActionResult<PrePlantingAssessmentResponse>> SubmitPrePlantingAssessment(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.SubmitPrePlantingAssessmentAsync(id, cancellationToken));

    [HttpPost("{id:guid}/run-field-analysis")]
    [Authorize(Roles = nameof(ApplicationRole.FieldOfficer))]
    public async Task<ActionResult<FieldAnalysisRunResponse>> RunFieldAnalysis(Guid id, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.RunFieldAnalysisAsync(id, cancellationToken));

    [HttpGet("{id:guid}/workflow-status")]
    public async Task<ActionResult<CropPlanningWorkflowStatusResponse>> GetWorkflowStatus(Guid id, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.GetWorkflowStatusAsync(id, cancellationToken));

    [HttpGet("{id:guid}/planning-result")]
    public async Task<ActionResult<CropPlanningResultResponse>> GetPlanningResult(Guid id, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.GetPlanningResultAsync(id, cancellationToken));

    [HttpGet("{id:guid}/field-analysis-result")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<FieldAnalysisOutput>> GetFieldAnalysisResult(Guid id, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.GetFieldAnalysisResultAsync(id, cancellationToken));

    [HttpGet("{id:guid}/member-3-handoff")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<Member3HandoffResponse>> GetMember3Handoff(Guid id, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.GetMember3HandoffAsync(id, cancellationToken));
}
