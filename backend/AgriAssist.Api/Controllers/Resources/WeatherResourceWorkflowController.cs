using AgriAssist.Api.Dtos.Resources;
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
    [HttpPost("{id:guid}/run-weather-resource-analysis")]
    [Authorize(Roles = $"{nameof(ApplicationRole.Admin)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.ResourceOfficer)}")]
    public async Task<ActionResult<WeatherResourceRunResponse>> Run(Guid id, CancellationToken cancellationToken) =>
        Ok(await workflowService.RunAsync(id, cancellationToken));

    [HttpGet("{id:guid}/weather-resource-result")]
    public async Task<ActionResult<WeatherResourceOutput>> Result(Guid id, CancellationToken cancellationToken) =>
        Ok(await workflowService.GetResultAsync(id, cancellationToken));
}
