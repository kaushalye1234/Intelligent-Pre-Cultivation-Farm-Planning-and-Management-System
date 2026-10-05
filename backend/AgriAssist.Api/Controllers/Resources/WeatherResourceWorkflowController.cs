using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriAssist.Api.Controllers.Resources;

/// <summary>Member 3 endpoints on the crop planning workflow.</summary>
[ApiController]
[Route("api/crop-plans")]
[Authorize]
public sealed class WeatherResourceWorkflowController(IWeatherResourceWorkflowService workflowService) : ControllerBase
{
    /// <summary>Crop plans whose latest workflow is waiting for Weather/Resource Analysis (read-only projection).</summary>
    [HttpGet("weather-resource-work-queue")]
    [Authorize(Roles = nameof(ApplicationRole.ResourceOfficer))]
    public async Task<ActionResult<PagedResult<WeatherResourceWorkItemResponse>>> WorkQueue([FromQuery] PagedQuery query, CancellationToken cancellationToken) =>
        Ok(await workflowService.GetWorkQueueAsync(query, cancellationToken));

    /// <summary>Weather/Resource analyses that were already run, newest first, each with a summary of its stored AI result.</summary>
    [HttpGet("weather-resource-history")]
    [Authorize(Roles = nameof(ApplicationRole.ResourceOfficer))]
    public async Task<ActionResult<PagedResult<WeatherResourceHistoryItemResponse>>> History([FromQuery] PagedQuery query, CancellationToken cancellationToken) =>
        Ok(await workflowService.GetHistoryAsync(query, cancellationToken));

    /// <summary>One history entry with the exact AI result stored for that workflow.</summary>
    [HttpGet("weather-resource-history/{workflowId:guid}")]
    [Authorize(Roles = nameof(ApplicationRole.ResourceOfficer))]
    public async Task<ActionResult<WeatherResourceHistoryDetailResponse>> HistoryEntry(Guid workflowId, CancellationToken cancellationToken) =>
        Ok(await workflowService.GetHistoryEntryAsync(workflowId, cancellationToken));

    [HttpPost("{id:guid}/run-weather-resource-analysis")]
    [Authorize(Roles = $"{nameof(ApplicationRole.Admin)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.ResourceOfficer)}")]
    public async Task<ActionResult<WeatherResourceRunResponse>> Run(Guid id, CancellationToken cancellationToken) =>
        Ok(await workflowService.RunAsync(id, cancellationToken));

    [HttpGet("{id:guid}/weather-resource-result")]
    public async Task<ActionResult<WeatherResourceOutput>> Result(Guid id, CancellationToken cancellationToken) =>
        Ok(await workflowService.GetResultAsync(id, cancellationToken));
}
