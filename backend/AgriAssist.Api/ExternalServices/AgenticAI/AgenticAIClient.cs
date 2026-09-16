using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Dtos.Resources;

namespace AgriAssist.Api.ExternalServices.AgenticAI;

public sealed class AgenticAIClient(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<AgenticAIClient> logger) : IAgenticAIClient, IWeatherResourceAIClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<CropPlanningCoordinatorOutput> RunCropPlanningCoordinatorAsync(CropPlanningCoordinatorInput input, CancellationToken cancellationToken) =>
        PostAsync<CropPlanningCoordinatorInput, CropPlanningCoordinatorOutput>(
            "/workflows/crop-planning/coordinator",
            input,
            input.WorkflowId,
            "crop planning coordinator",
            cancellationToken);

    public Task<FieldAnalysisOutput> RunFieldAnalysisAsync(FieldAnalysisInput input, CancellationToken cancellationToken) =>
        PostAsync<FieldAnalysisInput, FieldAnalysisOutput>(
            "/workflows/crop-planning/field-analysis",
            input,
            input.WorkflowId,
            "field analysis",
            cancellationToken);

    public Task<WeatherResourceOutput> RunWeatherResourceAnalysisAsync(WeatherResourceInput input, CancellationToken cancellationToken) =>
        PostAsync<WeatherResourceInput, WeatherResourceOutput>(
            "/workflows/crop-planning/weather-resource",
            input,
            input.WorkflowId,
            "weather and resource analysis",
            cancellationToken);

    private async Task<TOutput> PostAsync<TInput, TOutput>(
        string path,
        TInput input,
        Guid workflowId,
        string operationName,
        CancellationToken cancellationToken)
    {
        var serviceUrl = configuration["AI:ServiceUrl"];
        var serviceToken = configuration["AI:ServiceToken"];

        if (string.IsNullOrWhiteSpace(serviceUrl) || string.IsNullOrWhiteSpace(serviceToken))
        {
            throw new InvalidOperationException("AI service URL or token is not configured.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{serviceUrl.TrimEnd('/')}{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceToken);
        request.Content = JsonContent.Create(input, options: JsonOptions);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));

        using var response = await httpClient.SendAsync(request, timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("AI service returned {StatusCode} for {OperationName} workflow {WorkflowId}", response.StatusCode, operationName, workflowId);
            throw new InvalidOperationException($"AI service failed to complete the {operationName} step.");
        }

        var output = await response.Content.ReadFromJsonAsync<TOutput>(JsonOptions, timeout.Token);
        return output ?? throw new InvalidOperationException($"AI service returned an empty {operationName} response.");
    }
}
