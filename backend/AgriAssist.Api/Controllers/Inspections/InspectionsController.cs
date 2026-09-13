using AgriAssist.Api.Dtos.Inspections;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.Inspections;
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
    public async Task<ActionResult<PagedResult<FieldInspectionResponse>>> Search([FromQuery] PagedQuery query, [FromQuery] Guid? fieldId, [FromQuery] InspectionStatus? status, CancellationToken cancellationToken) =>
        Ok(await inspectionService.SearchInspectionsAsync(query, fieldId, status, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FieldInspectionDetailResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await inspectionService.GetInspectionAsync(id, cancellationToken));

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<InspectionHistoryEventResponse>>> History(Guid id, CancellationToken cancellationToken) =>
        Ok(await inspectionService.GetInspectionHistoryAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<FieldInspectionResponse>> Create(FieldInspectionRequest request, CancellationToken cancellationToken) =>
        Ok(await inspectionService.CreateInspectionAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<FieldInspectionResponse>> Update(Guid id, FieldInspectionRequest request, CancellationToken cancellationToken) =>
        Ok(await inspectionService.UpdateInspectionAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<FieldInspectionResponse>> Submit(Guid id, CancellationToken cancellationToken) =>
        Ok(await inspectionService.SubmitInspectionAsync(id, cancellationToken));

    [HttpPost("{id:guid}/close")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<FieldInspectionResponse>> Close(Guid id, CancellationToken cancellationToken) =>
        Ok(await inspectionService.CloseInspectionAsync(id, cancellationToken));

    [HttpGet("observations")]
    public async Task<ActionResult<PagedResult<ObservationResponse>>> SearchObservations([FromQuery] PagedQuery query, [FromQuery] Guid? inspectionId, CancellationToken cancellationToken) =>
        Ok(await inspectionService.SearchObservationsAsync(query, inspectionId, cancellationToken));

    [HttpPost("observations")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<ObservationResponse>> CreateObservation(ObservationRequest request, CancellationToken cancellationToken) =>
        Ok(await inspectionService.CreateObservationAsync(request, cancellationToken));

    [HttpGet("issues")]
    public async Task<ActionResult<PagedResult<CropIssueResponse>>> SearchIssues([FromQuery] PagedQuery query, [FromQuery] CropIssueSeverity? severity, [FromQuery] CropIssueStatus? status, CancellationToken cancellationToken) =>
        Ok(await inspectionService.SearchIssuesAsync(query, severity, status, cancellationToken));

    [HttpGet("issues/{id:guid}")]
    public async Task<ActionResult<CropIssueResponse>> GetIssue(Guid id, CancellationToken cancellationToken) =>
        Ok(await inspectionService.GetIssueAsync(id, cancellationToken));

    [HttpPost("issues")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<CropIssueResponse>> CreateIssue(CropIssueRequest request, CancellationToken cancellationToken) =>
        Ok(await inspectionService.CreateIssueAsync(request, cancellationToken));

    [HttpPatch("issues/{id:guid}/status")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<CropIssueResponse>> UpdateIssueStatus(Guid id, CropIssueStatusRequest request, CancellationToken cancellationToken) =>
        Ok(await inspectionService.UpdateIssueStatusAsync(id, request, cancellationToken));

    [HttpPost("issues/{id:guid}/escalate")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<CropIssueResponse>> EscalateIssue(Guid id, CancellationToken cancellationToken) =>
        Ok(await inspectionService.EscalateIssueAsync(id, cancellationToken));

    [HttpGet("recommendations")]
    public async Task<ActionResult<PagedResult<FollowUpRecommendationResponse>>> SearchRecommendations([FromQuery] PagedQuery query, [FromQuery] Guid? cropIssueId, [FromQuery] bool? isCompleted, CancellationToken cancellationToken) =>
        Ok(await inspectionService.SearchRecommendationsAsync(query, cropIssueId, isCompleted, cancellationToken));

    [HttpPost("recommendations")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<FollowUpRecommendationResponse>> CreateRecommendation(FollowUpRecommendationRequest request, CancellationToken cancellationToken) =>
        Ok(await inspectionService.CreateRecommendationAsync(request, cancellationToken));

    [HttpPatch("recommendations/{id:guid}")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<FollowUpRecommendationResponse>> UpdateRecommendation(Guid id, FollowUpRecommendationUpdateRequest request, CancellationToken cancellationToken) =>
        Ok(await inspectionService.UpdateRecommendationAsync(id, request, cancellationToken));

    [HttpGet("{id:guid}/images")]
    public async Task<ActionResult<IReadOnlyList<InspectionImageResponse>>> Images(Guid id, CancellationToken cancellationToken) =>
        Ok(await inspectionService.GetInspectionImagesAsync(id, cancellationToken));

    [HttpPost("{id:guid}/images")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<InspectionImageResponse>> UploadImage(Guid id, IFormFile file, CancellationToken cancellationToken) =>
        Ok(await inspectionService.UploadImageAsync(id, file, cancellationToken));
}
