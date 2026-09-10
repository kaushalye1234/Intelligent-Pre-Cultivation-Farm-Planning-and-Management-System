using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.CropPlanning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriAssist.Api.Controllers.CropPlanning;

[ApiController]
[Route("api/crop-planning")]
[Authorize]
public sealed class CropPlanningController(ICropPlanningService cropPlanningService) : ControllerBase
{
    [HttpGet("farms")]
    public async Task<ActionResult<PagedResult<FarmResponse>>> SearchFarms([FromQuery] PagedQuery query, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.SearchFarmsAsync(query, cancellationToken));

    [HttpGet("farms/{id:guid}")]
    public async Task<ActionResult<FarmResponse>> GetFarm(Guid id, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.GetFarmAsync(id, cancellationToken));

    [HttpPost("farms")]
    [Authorize(Roles = $"{nameof(ApplicationRole.Farmer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<FarmResponse>> CreateFarm(FarmRequest request, CancellationToken cancellationToken)
    {
        var response = await cropPlanningService.CreateFarmAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetFarm), new { id = response.Id }, response);
    }

    [HttpPut("farms/{id:guid}")]
    [Authorize(Roles = $"{nameof(ApplicationRole.Farmer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<FarmResponse>> UpdateFarm(Guid id, FarmRequest request, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.UpdateFarmAsync(id, request, cancellationToken));

    [HttpDelete("farms/{id:guid}")]
    [Authorize(Roles = $"{nameof(ApplicationRole.Farmer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<IActionResult> DeleteFarm(Guid id, CancellationToken cancellationToken)
    {
        await cropPlanningService.DeleteFarmAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("fields")]
    public async Task<ActionResult<PagedResult<FieldResponse>>> SearchFields([FromQuery] PagedQuery query, [FromQuery] Guid? farmId, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.SearchFieldsAsync(query, farmId, cancellationToken));

    [HttpPost("fields")]
    [Authorize(Roles = $"{nameof(ApplicationRole.Farmer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<FieldResponse>> CreateField(FieldRequest request, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.CreateFieldAsync(request, cancellationToken));

    [HttpPut("fields/{id:guid}")]
    [Authorize(Roles = $"{nameof(ApplicationRole.Farmer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<FieldResponse>> UpdateField(Guid id, FieldRequest request, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.UpdateFieldAsync(id, request, cancellationToken));

    [HttpGet("crop-types")]
    public async Task<ActionResult<PagedResult<CropTypeResponse>>> SearchCropTypes([FromQuery] PagedQuery query, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.SearchCropTypesAsync(query, cancellationToken));

    [HttpPost("crop-types")]
    [Authorize(Roles = $"{nameof(ApplicationRole.Admin)},{nameof(ApplicationRole.AgriculturalOfficer)}")]
    public async Task<ActionResult<CropTypeResponse>> CreateCropType(CropTypeRequest request, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.CreateCropTypeAsync(request, cancellationToken));

    [HttpPut("crop-types/{id:guid}")]
    [Authorize(Roles = $"{nameof(ApplicationRole.Admin)},{nameof(ApplicationRole.AgriculturalOfficer)}")]
    public async Task<ActionResult<CropTypeResponse>> UpdateCropType(Guid id, CropTypeRequest request, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.UpdateCropTypeAsync(id, request, cancellationToken));

    [HttpGet("crop-cycles")]
    public async Task<ActionResult<PagedResult<CropCycleResponse>>> SearchCropCycles([FromQuery] PagedQuery query, [FromQuery] Guid? fieldId, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.SearchCropCyclesAsync(query, fieldId, cancellationToken));

    [HttpPost("crop-cycles")]
    [Authorize(Roles = $"{nameof(ApplicationRole.Farmer)},{nameof(ApplicationRole.Admin)},{nameof(ApplicationRole.AgriculturalOfficer)}")]
    public async Task<ActionResult<CropCycleResponse>> CreateCropCycle(CropCycleRequest request, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.CreateCropCycleAsync(request, cancellationToken));

    [HttpGet("requests")]
    public async Task<ActionResult<PagedResult<CropPlanRequestResponse>>> SearchRequests([FromQuery] PagedQuery query, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.SearchCropPlanRequestsAsync(query, cancellationToken));

    [HttpPost("requests")]
    [Authorize(Roles = $"{nameof(ApplicationRole.Farmer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<CropPlanRequestResponse>> CreateRequest(CropPlanRequestCreate request, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.CreateCropPlanRequestAsync(request, cancellationToken));

    [HttpPut("requests/{id:guid}")]
    [Authorize(Roles = $"{nameof(ApplicationRole.Farmer)},{nameof(ApplicationRole.Admin)},{nameof(ApplicationRole.AgriculturalOfficer)}")]
    public async Task<ActionResult<CropPlanRequestResponse>> UpdateRequest(Guid id, CropPlanRequestUpdate request, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.UpdateCropPlanRequestAsync(id, request, cancellationToken));

    [HttpPost("requests/preliminary")]
    [Authorize(Roles = $"{nameof(ApplicationRole.Farmer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<CropPlanRequestResponse>> GeneratePreliminary(CropPlanRequestCreate request, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.GeneratePreliminaryRequestAsync(request, cancellationToken));

    [HttpGet("requests/{id:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<CropPlanHistoryResponse>>> GetHistory(Guid id, CancellationToken cancellationToken) =>
        Ok(await cropPlanningService.GetCropPlanHistoryAsync(id, cancellationToken));
}
