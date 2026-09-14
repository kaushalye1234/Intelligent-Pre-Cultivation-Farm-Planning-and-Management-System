namespace AgriAssist.Api.ExternalServices.Weather;

public sealed class WeatherOptions
{
    public string BaseUrl { get; set; } = "https://api.openweathermap.org/data/2.5";
    public string ApiKey { get; set; } = string.Empty;
}
