<<<<<<< Updated upstream
namespace AgriAssist.Api.ExternalServices.AgenticAI;

public sealed class AgenticAIClient(ILogger<AgenticAIClient> logger) : IAgenticAIClient
{
    public Task StartWorkflowAsync(Guid workflowId, CancellationToken cancellationToken)
=======
﻿using System.Net.Http.Headers;
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
>>>>>>> Stashed changes
    {
        logger.LogInformation("Agentic AI client is disabled for Prompt 01. Workflow {WorkflowId} was not executed.", workflowId);
        throw new NotSupportedException("Agentic AI execution is intentionally disabled in Prompt 01.");
    }
}
