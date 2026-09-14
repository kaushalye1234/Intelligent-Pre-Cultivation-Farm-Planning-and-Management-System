namespace AgriAssist.Api.ExternalServices.Weather;

public interface IWeatherService
{
    /// <summary>
    /// Returns a daily forecast for a place name. Never throws for provider problems:
    /// failures come back with <c>IsAvailable = false</c> and a safe message.
    /// </summary>
    Task<WeatherForecastResponse> GetForecastAsync(string location, CancellationToken cancellationToken);
}

public sealed record WeatherDayResponse(
    DateOnly Date,
    decimal MinTemperatureC,
    decimal MaxTemperatureC,
    decimal RainMm,
    decimal MaxWindSpeedMs,
    string Description);

public sealed record WeatherForecastResponse(
    string Location,
    bool IsAvailable,
    string Message,
    IReadOnlyList<WeatherDayResponse> Days)
{
    public static WeatherForecastResponse Unavailable(string location, string message) => new(location, false, message, []);
}
