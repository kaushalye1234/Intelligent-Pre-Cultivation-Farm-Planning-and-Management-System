using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AgriAssist.Api.ExternalServices.Weather;

/// <summary>
/// OpenWeatherMap 5 day / 3 hour forecast, grouped into days.
/// Forecast data is never invented: any failure returns an unavailable result.
/// </summary>
public sealed class WeatherService(HttpClient httpClient, IOptions<WeatherOptions> options, ILogger<WeatherService> logger) : IWeatherService
{
    public async Task<WeatherForecastResponse> GetForecastAsync(string location, CancellationToken cancellationToken)
    {
        location = location?.Trim() ?? string.Empty;
        if (location.Length == 0) return WeatherForecastResponse.Unavailable(location, "A location is required for the weather forecast.");

        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            return WeatherForecastResponse.Unavailable(location, "Weather service is not configured.");
        }

        // Farm locations are often "Town, District"; fall back to the first part if the full text is not recognised.
        var candidates = new[] { location, location.Split(',')[0].Trim() }.Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            try
            {
                var url = $"{settings.BaseUrl.TrimEnd('/')}/forecast?q={Uri.EscapeDataString(candidate)}&units=metric&appid={Uri.EscapeDataString(settings.ApiKey)}";
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                using var response = await httpClient.GetAsync(url, timeout.Token);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound) continue;
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Weather provider returned {StatusCode}", response.StatusCode);
                    return WeatherForecastResponse.Unavailable(location, "Weather provider is currently unavailable.");
                }

                await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
                var days = ParseDays(document.RootElement);
                return days.Count == 0
                    ? WeatherForecastResponse.Unavailable(location, "Weather provider returned no forecast data.")
                    : new WeatherForecastResponse(location, true, "Forecast from OpenWeatherMap.", days);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or KeyNotFoundException)
            {
                // Do not log the exception message: it can contain the request URL with the API key.
                logger.LogWarning("Weather forecast request failed ({ExceptionType})", exception.GetType().Name);
                if (cancellationToken.IsCancellationRequested) throw;
                return WeatherForecastResponse.Unavailable(location, "Weather provider is currently unavailable.");
            }
        }

        return WeatherForecastResponse.Unavailable(location, $"No weather forecast was found for '{location}'.");
    }

    public static IReadOnlyList<WeatherDayResponse> ParseDays(JsonElement root)
    {
        if (!root.TryGetProperty("list", out var list) || list.ValueKind != JsonValueKind.Array) return [];

        return list.EnumerateArray()
            .Select(item => new
            {
                Date = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(item.GetProperty("dt").GetInt64()).UtcDateTime),
                Min = item.GetProperty("main").GetProperty("temp_min").GetDecimal(),
                Max = item.GetProperty("main").GetProperty("temp_max").GetDecimal(),
                Rain = item.TryGetProperty("rain", out var rain) && rain.TryGetProperty("3h", out var rain3h) ? rain3h.GetDecimal() : 0m,
                Wind = item.TryGetProperty("wind", out var wind) && wind.TryGetProperty("speed", out var speed) ? speed.GetDecimal() : 0m,
                Description = item.TryGetProperty("weather", out var weather) && weather.GetArrayLength() > 0
                    ? weather[0].GetProperty("description").GetString() ?? string.Empty
                    : string.Empty
            })
            .GroupBy(item => item.Date)
            .OrderBy(group => group.Key)
            .Select(group => new WeatherDayResponse(
                group.Key,
                Math.Round(group.Min(item => item.Min), 1),
                Math.Round(group.Max(item => item.Max), 1),
                Math.Round(group.Sum(item => item.Rain), 1),
                Math.Round(group.Max(item => item.Wind), 1),
                group.GroupBy(item => item.Description).OrderByDescending(g => g.Count()).First().Key))
            .ToList();
    }
}
