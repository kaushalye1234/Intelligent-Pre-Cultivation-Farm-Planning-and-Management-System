using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Dtos.TaskApproval;

namespace AgriAssist.Api.ExternalServices.AgenticAI;

public sealed class AgenticAIClient(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<AgenticAIClient> logger) : IAgenticAIClient, IWeatherResourceAIClient, ISchedulingValidationAIClient, ICropFindingAIClient, IInspectionAssistanceAIClient
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

    public Task<SchedulingValidationOutput> RunSchedulingValidationAsync(SchedulingValidationInput input, CancellationToken cancellationToken) =>
        PostAsync<SchedulingValidationInput, SchedulingValidationOutput>(
            "/workflows/crop-planning/scheduling-validation",
            input,
            input.WorkflowId,
            "scheduling validation",
            cancellationToken);

    public Task<CropSuggestionsResponse> SuggestCropsAsync(SuggestCropsInput input, CancellationToken cancellationToken) =>
        PostCropFindingAsync<SuggestCropsInput, CropSuggestionsResponse>(
            "/crop-finding/suggest-crops",
            input,
            "suggest crops",
            cancellationToken);

    public Task<VarietySuggestionsResponse> SuggestVarietiesAsync(SuggestVarietiesInput input, CancellationToken cancellationToken) =>
        PostCropFindingAsync<SuggestVarietiesInput, VarietySuggestionsResponse>(
            "/crop-finding/suggest-varieties",
            input,
            "suggest varieties",
            cancellationToken);

    public Task<ReferenceDiscoveryResponse> DiscoverReferencesAsync(DiscoverReferencesInput input, CancellationToken cancellationToken) =>
        PostCropFindingAsync<DiscoverReferencesInput, ReferenceDiscoveryResponse>(
            "/crop-finding/discover-references",
            input,
            "discover references",
            cancellationToken);

    public Task<InspectionNoteAssistanceResponse> GenerateNoteSuggestionsAsync(
        InspectionNoteAssistanceAiInput input,
        CancellationToken cancellationToken) =>
        PostAssistanceAsync<InspectionNoteAssistanceAiInput, InspectionNoteAssistanceResponse>(
            "/workflows/crop-planning/inspection-note-assistance",
            input,
            "inspection note assistance",
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

    private async Task<TOutput> PostCropFindingAsync<TInput, TOutput>(
        string path,
        TInput input,
        string operationName,
        CancellationToken cancellationToken)
    {
        var serviceUrl = configuration["AI:ServiceUrl"];
        var serviceToken = configuration["AI:ServiceToken"];
        if (string.IsNullOrWhiteSpace(serviceUrl) || string.IsNullOrWhiteSpace(serviceToken))
            throw new InvalidOperationException("AI service URL or token is not configured.");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{serviceUrl.TrimEnd('/')}{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceToken);
        request.Content = JsonContent.Create(input, options: JsonOptions);

        var timeoutSeconds = Math.Clamp(configuration.GetValue<int?>("AI:CropFindingTimeoutSeconds") ?? 110, 10, 180);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        using var response = await httpClient.SendAsync(request, timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "AI service returned {StatusCode} for CropFinding operation {OperationName}",
                response.StatusCode,
                operationName);
            throw new HttpRequestException(
                $"AI service failed to {operationName}.",
                null,
                response.StatusCode);
        }

        var output = await response.Content.ReadFromJsonAsync<TOutput>(JsonOptions, timeout.Token);
        return output ?? throw new InvalidOperationException($"AI service returned an empty {operationName} response.");
    }

    private async Task<TOutput> PostAssistanceAsync<TInput, TOutput>(
        string path,
        TInput input,
        string operationName,
        CancellationToken cancellationToken)
    {
        var serviceUrl = configuration["AI:ServiceUrl"];
        var serviceToken = configuration["AI:ServiceToken"];
        if (string.IsNullOrWhiteSpace(serviceUrl) || string.IsNullOrWhiteSpace(serviceToken))
            throw new InvalidOperationException("AI service URL or token is not configured.");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{serviceUrl.TrimEnd('/')}{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceToken);
        request.Content = JsonContent.Create(input, options: JsonOptions);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(35));
        using var response = await httpClient.SendAsync(request, timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("AI service returned {StatusCode} for {OperationName}", response.StatusCode, operationName);
            throw new HttpRequestException($"AI service failed to complete {operationName}.", null, response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<TOutput>(JsonOptions, timeout.Token)
            ?? throw new InvalidOperationException($"AI service returned an empty {operationName} response.");
    }
}
