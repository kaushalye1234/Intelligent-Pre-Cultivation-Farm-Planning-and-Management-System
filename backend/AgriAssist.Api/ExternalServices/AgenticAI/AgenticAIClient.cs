using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgriAssist.Api.Dtos.CropPlanning;

namespace AgriAssist.Api.ExternalServices.AgenticAI;

public sealed class AgenticAIClient(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<AgenticAIClient> logger) : IAgenticAIClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CropPlanningCoordinatorOutput> RunCropPlanningCoordinatorAsync(CropPlanningCoordinatorInput input, CancellationToken cancellationToken)
    {
        var serviceUrl = configuration["AI:ServiceUrl"];
        var serviceToken = configuration["AI:ServiceToken"];

        if (string.IsNullOrWhiteSpace(serviceUrl) || string.IsNullOrWhiteSpace(serviceToken))
        {
            throw new InvalidOperationException("AI service URL or token is not configured.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{serviceUrl.TrimEnd('/')}/workflows/crop-planning/coordinator");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceToken);
        request.Content = JsonContent.Create(input, options: JsonOptions);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));

        using var response = await httpClient.SendAsync(request, timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("AI service returned {StatusCode} for workflow {WorkflowId}", response.StatusCode, input.WorkflowId);
            throw new InvalidOperationException("AI service failed to complete the crop planning coordinator step.");
        }

        var output = await response.Content.ReadFromJsonAsync<CropPlanningCoordinatorOutput>(JsonOptions, timeout.Token);
        return output ?? throw new InvalidOperationException("AI service returned an empty crop planning response.");
    }
}
