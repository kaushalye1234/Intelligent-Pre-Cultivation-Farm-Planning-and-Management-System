using AgriAssist.Api.Dtos.Resources;

namespace AgriAssist.Api.ExternalServices.AgenticAI;

/// <summary>Member 3 call to the AI service. Kept separate so existing IAgenticAIClient implementations are unaffected.</summary>
public interface IWeatherResourceAIClient
{
    Task<WeatherResourceOutput> RunWeatherResourceAnalysisAsync(WeatherResourceInput input, CancellationToken cancellationToken);
}
