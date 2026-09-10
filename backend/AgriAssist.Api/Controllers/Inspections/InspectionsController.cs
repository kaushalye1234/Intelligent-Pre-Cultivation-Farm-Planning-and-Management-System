using AgriAssist.Api.Dtos.Inspections;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.Inspections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriAssist.Api.Controllers.Inspections;

[ApiController]
[Route("api/inspections")]
[Authorize]
public sealed class InspectionsController(IInspectionService inspectionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<FieldInspectionResponse>>> Search([FromQuery] PagedQuery query, [FromQuery] Guid? fieldId, CancellationToken cancellationToken) =>
        Ok(await inspectionService.SearchInspectionsAsync(query, fieldId, cancellationToken));

    [HttpPost]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<FieldInspectionResponse>> Create(FieldInspectionRequest request, CancellationToken cancellationToken) =>
        Ok(await inspectionService.CreateInspectionAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<FieldInspectionResponse>> Update(Guid id, FieldInspectionRequest request, CancellationToken cancellationToken) =>
        Ok(await inspectionService.UpdateInspectionAsync(id, request, cancellationToken));

    [HttpGet("observations")]
    public async Task<ActionResult<PagedResult<ObservationResponse>>> SearchObservations([FromQuery] PagedQuery query, [FromQuery] Guid? inspectionId, CancellationToken cancellationToken) =>
        Ok(await inspectionService.SearchObservationsAsync(query, inspectionId, cancellationToken));

    [HttpPost("observations")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<ObservationResponse>> CreateObservation(ObservationRequest request, CancellationToken cancellationToken) =>
        Ok(await inspectionService.CreateObservationAsync(request, cancellationToken));

    [HttpGet("issues")]
    public async Task<ActionResult<PagedResult<CropIssueResponse>>> SearchIssues([FromQuery] PagedQuery query, CancellationToken cancellationToken) =>
        Ok(await inspectionService.SearchIssuesAsync(query, cancellationToken));

    [HttpPost("issues")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<CropIssueResponse>> CreateIssue(CropIssueRequest request, CancellationToken cancellationToken) =>
        Ok(await inspectionService.CreateIssueAsync(request, cancellationToken));

    [HttpPost("issues/{id:guid}/escalate")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<CropIssueResponse>> EscalateIssue(Guid id, CancellationToken cancellationToken) =>
        Ok(await inspectionService.EscalateIssueAsync(id, cancellationToken));

    [HttpPost("recommendations")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<FollowUpRecommendationResponse>> CreateRecommendation(FollowUpRecommendationRequest request, CancellationToken cancellationToken) =>
        Ok(await inspectionService.CreateRecommendationAsync(request, cancellationToken));

    [HttpPost("{id:guid}/images")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<InspectionImageResponse>> UploadImage(Guid id, IFormFile file, CancellationToken cancellationToken) =>
        Ok(await inspectionService.UploadImageAsync(id, file, cancellationToken));
}
