using AgriAssist.Api.ExternalServices.Weather;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriAssist.Api.Controllers.Resources;

[ApiController]
[Route("api/weather")]
[Authorize]
public sealed class WeatherController(IWeatherService weatherService) : ControllerBase
{
    [HttpGet("forecast")]
    public async Task<ActionResult<WeatherForecastResponse>> Forecast([FromQuery] string location, CancellationToken cancellationToken) =>
        Ok(await weatherService.GetForecastAsync(location, cancellationToken));
}
