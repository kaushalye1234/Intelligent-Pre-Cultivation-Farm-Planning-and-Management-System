using System.Net;
using System.Text.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.ExternalServices.AgenticAI;
using AgriAssist.Api.ExternalServices.Weather;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.CropPlanning;
using AgriAssist.Api.Services.Shared;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Services.Resources;

public interface IWeatherResourceWorkflowService
{
    Task<WeatherResourceRunResponse> RunAsync(Guid cropPlanRequestId, CancellationToken cancellationToken);
    Task<WeatherResourceOutput> GetResultAsync(Guid cropPlanRequestId, CancellationToken cancellationToken);
}

/// <summary>
/// Member 3 step of the crop planning workflow. Runs after Member 2's field analysis:
/// gathers the weather forecast and an inventory snapshot, asks the WeatherResourceAgent for
/// an assessment, validates it against the snapshot, stores it and hands off to Member 4.
/// </summary>
public sealed class WeatherResourceWorkflowService(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    ICropPlanningService cropPlanningService,
    IWeatherService weatherService,
    IWeatherResourceAIClient aiClient) : IWeatherResourceWorkflowService
{
    public const string AgentName = "WeatherResourceAgent";
    public const string StepName = "WeatherResourceAnalysis";
    public const string NextAgentName = "SchedulingValidationAgent";
    private const int MaxStocksSentToAgent = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] AllowedStatuses = ["Analyzed", "SafeFailure"];
    private static readonly string[] AllowedRisks = ["Low", "Medium", "High", "Unknown"];

    public async Task<WeatherResourceRunResponse> RunAsync(Guid cropPlanRequestId, CancellationToken cancellationToken)
    {
        // Also enforces crop plan request access and gives us Member 2's output.
        var handoff = await cropPlanningService.GetMember3HandoffAsync(cropPlanRequestId, cancellationToken);
        var workflow = await LoadWorkflowAsync(cropPlanRequestId, includeFarm: true, cancellationToken);
        var step = FindStep(workflow);

        if (step.Status == AgentStepStatus.Completed)
        {
            var stored = ReadOutput(step.OutputJson, workflow.Id);
            return ToRunResponse(workflow, step, stored);
        }

        if (step.Status == AgentStepStatus.Running)
        {
            throw new ApiException(HttpStatusCode.Conflict, "WEATHER_RESOURCE_ALREADY_RUNNING", "Weather and resource analysis is already running for this workflow.");
        }

        if (workflow.CurrentStep != AgentName)
        {
            throw new ApiException(HttpStatusCode.Conflict, "FIELD_ANALYSIS_NOT_COMPLETED", "Field analysis must be completed before weather and resource analysis can run.");
        }

        var location = workflow.CropPlanRequest?.Farm?.Location ?? string.Empty;
        var weather = await weatherService.GetForecastAsync(location, cancellationToken);
        var stocks = await LoadStockSnapshotAsync(cancellationToken);
        var input = new WeatherResourceInput(
            workflow.Id,
            step.Id,
            cropPlanRequestId,
            location,
            handoff.PreferredStartDate,
            handoff.PreferredEndDate,
            handoff.Priority,
            handoff.FieldAnalysisSummary,
            weather,
            stocks);

        var userId = currentUser.UserId ?? throw new ApiException(HttpStatusCode.Unauthorized, "AUTH_REQUIRED", "Authentication is required.");
        step.InputJson = JsonSerializer.Serialize(input, JsonOptions);
        step.Status = AgentStepStatus.Running;
        step.StartedAt = DateTime.UtcNow;
        step.ErrorCode = null;
        step.ErrorMessageSafe = null;
        workflow.Status = AgentWorkflowStatus.Running;
        await dbContext.SaveChangesAsync(cancellationToken);

        WeatherResourceOutput output;
        IReadOnlyList<string> validationErrors;
        string validatorName;
        try
        {
            output = await aiClient.RunWeatherResourceAnalysisAsync(input, cancellationToken);
            validationErrors = Validate(output, input);
            validatorName = "WeatherResourceOutputValidator";
            if (validationErrors.Count > 0) output = SafeFailure(workflow.Id, validationErrors);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            validationErrors = ["AI service is unavailable or timed out during weather and resource analysis. No assessment was generated."];
            validatorName = "WeatherResourceAvailability";
            output = SafeFailure(workflow.Id, validationErrors);
        }

        var succeeded = !output.Status.Equals("SafeFailure", StringComparison.OrdinalIgnoreCase);
        step.OutputJson = JsonSerializer.Serialize(output, JsonOptions);
        step.Status = succeeded ? AgentStepStatus.Completed : AgentStepStatus.Failed;
        step.CompletedAt = DateTime.UtcNow;
        step.ErrorCode = succeeded ? null : validatorName == "WeatherResourceAvailability" ? "AI_SERVICE_UNAVAILABLE" : validationErrors.Count > 0 ? "AI_RESPONSE_INVALID" : "AI_SAFE_FAILURE";
        step.ErrorMessageSafe = succeeded ? null : output.Warnings.FirstOrDefault();

        dbContext.AgentValidationResults.Add(new AgentValidationResult
        {
            AgentWorkflowId = workflow.Id,
            ValidatorName = validatorName,
            IsValid = validationErrors.Count == 0,
            ErrorsJson = JsonSerializer.Serialize(validationErrors, JsonOptions),
            CreatedByUserId = userId
        });

        if (succeeded)
        {
            workflow.Status = AgentWorkflowStatus.Pending;
            workflow.CurrentStep = NextAgentName;
        }
        else
        {
            workflow.Status = AgentWorkflowStatus.Failed;
            workflow.CurrentStep = "SafeFailure";
            workflow.CompletedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToRunResponse(workflow, step, output);
    }

    public async Task<WeatherResourceOutput> GetResultAsync(Guid cropPlanRequestId, CancellationToken cancellationToken)
    {
        // Access check (throws NotFound when the caller cannot see the request).
        await cropPlanningService.GetWorkflowStatusAsync(cropPlanRequestId, cancellationToken);
        var workflow = await LoadWorkflowAsync(cropPlanRequestId, includeFarm: false, cancellationToken);
        return ReadOutput(FindStep(workflow).OutputJson, workflow.Id);
    }

    /// <summary>The agent may only describe resources and numbers it was given.</summary>
    public static IReadOnlyList<string> Validate(WeatherResourceOutput output, WeatherResourceInput input)
    {
        var errors = new List<string>();
        if (output.WorkflowId != input.WorkflowId) errors.Add("WeatherResource workflowId does not match the persisted workflow.");
        if (!AllowedStatuses.Contains(output.Status)) errors.Add("WeatherResource status must be Analyzed or SafeFailure.");
        if (!AllowedRisks.Contains(output.WeatherRisk)) errors.Add("WeatherResource weatherRisk must be Low, Medium, High or Unknown.");
        if (output.Warnings is null || output.ResourceChecks is null || output.Recommendations is null)
        {
            errors.Add("WeatherResource warnings, resourceChecks and recommendations arrays are required.");
            return errors;
        }

        if (output.Status == "SafeFailure" && !output.RequiresHumanReview) errors.Add("WeatherResource safe failures must require human review.");
        if (!input.Weather.IsAvailable && output.WeatherRisk != "Unknown") errors.Add("WeatherResource reported a weather risk without forecast data.");

        var stocks = input.Stocks.ToDictionary(stock => stock.InventoryStockId);
        foreach (var check in output.ResourceChecks)
        {
            if (!stocks.TryGetValue(check.InventoryStockId, out var stock))
            {
                errors.Add($"WeatherResource referenced unknown inventory stock ID {check.InventoryStockId}.");
            }
            else if (check.ResourceId != stock.ResourceId || check.AvailableQuantity != stock.AvailableQuantity)
            {
                errors.Add($"WeatherResource figures for inventory stock {check.InventoryStockId} do not match the inventory snapshot.");
            }
        }

        return errors;
    }

    private async Task<IReadOnlyList<StockSnapshot>> LoadStockSnapshotAsync(CancellationToken cancellationToken) =>
        await dbContext.InventoryStocks.AsNoTracking()
            .Where(stock => !stock.IsDeleted && stock.Resource != null && stock.Resource.IsActive && !stock.Resource.IsDeleted)
            .OrderBy(stock => stock.Resource!.Name)
            .Take(MaxStocksSentToAgent)
            .Select(stock => new StockSnapshot(
                stock.Id,
                stock.ResourceId,
                stock.Resource!.Name,
                stock.Resource.Unit,
                stock.QuantityOnHand,
                stock.ReservedQuantity,
                stock.QuantityOnHand - stock.ReservedQuantity,
                stock.LowStockThreshold))
            .ToListAsync(cancellationToken);

    private async Task<AgentWorkflow> LoadWorkflowAsync(Guid cropPlanRequestId, bool includeFarm, CancellationToken cancellationToken)
    {
        var query = dbContext.AgentWorkflows
            .Where(workflow => workflow.CropPlanRequestId == cropPlanRequestId && !workflow.IsDeleted)
            .OrderByDescending(workflow => workflow.CreatedAt)
            .ThenByDescending(workflow => workflow.Id)
            .Include(workflow => workflow.Steps)
            .AsQueryable();
        if (includeFarm) query = query.Include(workflow => workflow.CropPlanRequest)!.ThenInclude(request => request!.Farm);

        return await query.FirstOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(HttpStatusCode.NotFound, "NOT_FOUND", "AI workflow was not found.");
    }

    private static AgentStep FindStep(AgentWorkflow workflow) =>
        workflow.Steps.FirstOrDefault(step => step.AgentName == AgentName && step.StepName == StepName)
        ?? throw new ApiException(HttpStatusCode.NotFound, "NOT_FOUND", "Weather and resource analysis step was not found.");

    private static WeatherResourceRunResponse ToRunResponse(AgentWorkflow workflow, AgentStep step, WeatherResourceOutput output) =>
        new(workflow.Id, workflow.CropPlanRequestId ?? Guid.Empty, step.Id, output.Status, output.WeatherRisk, output.RequiresHumanReview, output.Warnings);

    private static WeatherResourceOutput SafeFailure(Guid workflowId, IEnumerable<string> warnings) =>
        new(workflowId, "SafeFailure", true, warnings.ToArray(), "Unknown", string.Empty, [], []);

    private static WeatherResourceOutput ReadOutput(string? outputJson, Guid workflowId)
    {
        if (string.IsNullOrWhiteSpace(outputJson)) return SafeFailure(workflowId, ["Weather and resource analysis output is not available yet."]);
        try
        {
            return JsonSerializer.Deserialize<WeatherResourceOutput>(outputJson, JsonOptions)
                ?? SafeFailure(workflowId, ["Weather and resource analysis output is empty."]);
        }
        catch (JsonException)
        {
            return SafeFailure(workflowId, ["Weather and resource analysis output could not be read safely."]);
        }
    }
}
