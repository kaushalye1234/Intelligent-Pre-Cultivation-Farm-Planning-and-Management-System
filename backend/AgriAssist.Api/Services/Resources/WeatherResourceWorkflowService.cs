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
/// Member 3 step of the crop planning workflow. Runs after Member 2's field analysis: sends the crop plan
/// context to the WeatherResourceAgent, which gathers verified requirements, field, inventory, reservations
/// and weather through the read-only internal agent tools. The output is validated against the tool results
/// the backend itself recorded for this step, stored, and handed off to Member 4. Nothing is reserved here.
/// </summary>
public sealed class WeatherResourceWorkflowService(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    ICropPlanningService cropPlanningService,
    IWeatherResourceAIClient aiClient) : IWeatherResourceWorkflowService
{
    public const string AgentName = "WeatherResourceAgent";
    public const string StepName = "WeatherResourceAnalysis";
    public const string NextAgentName = "SchedulingValidationAgent";
    private const decimal QuantityTolerance = 0.0005m;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] AllowedStatuses = ["Analyzed", "SafeFailure"];
    private static readonly string[] AllowedRisks = ["Low", "Medium", "High", "Unknown"];

    public async Task<WeatherResourceRunResponse> RunAsync(Guid cropPlanRequestId, CancellationToken cancellationToken)
    {
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

        // Enforces exact crop-plan access/stage and returns only the safe completed Member 2 output.
        var handoff = await cropPlanningService.GetMember3HandoffAsync(cropPlanRequestId, cancellationToken);
        var location = workflow.CropPlanRequest?.Farm?.Location ?? string.Empty;
        var input = new WeatherResourceInput(
            workflow.Id,
            step.Id,
            cropPlanRequestId,
            handoff.FieldId,
            location,
            handoff.PreferredStartDate,
            handoff.PreferredEndDate,
            handoff.Priority,
            handoff.FieldAnalysisSummary,
            new Member2FieldAnalysisContext(
                handoff.FieldSuitability,
                handoff.SoilAssessment,
                handoff.WaterAssessment,
                handoff.DrainageAssessment,
                handoff.FieldPreparationRequirements,
                handoff.PlantingReadiness,
                handoff.IdentifiedRisks,
                handoff.RecommendedPrePlantingActions,
                handoff.Priority,
                handoff.Warnings,
                handoff.RequiresHumanReview));

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
            var evidence = await LoadToolEvidenceAsync(step, workflow.Id, cancellationToken);
            validationErrors = Validate(output, evidence, workflow.Id);
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

    /// <summary>
    /// The agent may only describe what the backend tools returned for this step: stock figures, verified
    /// requirement quantities and the forecast. Every derived figure (shortage, sufficiency, statuses) must follow.
    /// </summary>
    public static IReadOnlyList<string> Validate(WeatherResourceOutput output, WeatherResourceToolEvidence evidence, Guid workflowId)
    {
        var errors = new List<string>();
        if (output.WorkflowId != workflowId) errors.Add("WeatherResource workflowId does not match the persisted workflow.");
        if (!AllowedStatuses.Contains(output.Status)) errors.Add("WeatherResource status must be Analyzed or SafeFailure.");
        if (!AllowedRisks.Contains(output.WeatherRisk)) errors.Add("WeatherResource weatherRisk must be Low, Medium, High or Unknown.");
        if (output.Warnings is null || output.ResourceChecks is null || output.Recommendations is null)
        {
            errors.Add("WeatherResource warnings, resourceChecks and recommendations arrays are required.");
            return errors;
        }

        if (output.Status == "SafeFailure")
        {
            if (!output.RequiresHumanReview) errors.Add("WeatherResource safe failures must require human review.");
            return errors;
        }

        var weatherAvailable = evidence.Weather is { IsAvailable: true, Days.Count: > 0 };
        if (!weatherAvailable && output.WeatherRisk != "Unknown") errors.Add("WeatherResource reported a weather risk without forecast data.");
        if (output.WeatherRisk is "High" or "Unknown" && !output.RequiresHumanReview) errors.Add("WeatherResource High or Unknown weather risk must require human review.");

        if (!evidence.AvailabilityRetrieved) errors.Add("WeatherResource did not retrieve inventory through GetResourceAvailability.");
        var stocks = evidence.Stocks.GroupBy(stock => stock.InventoryStockId).ToDictionary(group => group.Key, group => group.Last());
        var stocksByResource = evidence.Stocks.GroupBy(stock => stock.ResourceId).ToDictionary(group => group.Key, group => group.Last());
        var rules = evidence.Requirements?.Requirements ?? [];
        var comparable = rules
            .Where(rule => rule.Status == RequirementCalculationStatus.Calculated && rule.ResourceMatch == ResourceMatchStatus.Matched && rule.ResourceId.HasValue)
            .GroupBy(rule => rule.ResourceId!.Value)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var check in output.ResourceChecks)
        {
            if (!stocks.TryGetValue(check.InventoryStockId, out var stock))
            {
                errors.Add($"WeatherResource referenced unknown inventory stock ID {check.InventoryStockId}.");
                continue;
            }

            if (check.ResourceId != stock.ResourceId || !Same(check.AvailableQuantity, stock.AvailableQuantity))
                errors.Add($"WeatherResource figures for inventory stock {check.InventoryStockId} do not match the inventory snapshot.");
            if (check.IsLowStock != stock.AvailableQuantity <= stock.LowStockThreshold)
                errors.Add($"WeatherResource low-stock flag for inventory stock {check.InventoryStockId} does not follow from its threshold.");

            var (requested, sufficient, status) = comparable.TryGetValue(stock.ResourceId, out var rule)
                ? UnitsMatch(rule.ResourceUnit, stock.Unit)
                    ? (rule.RequiredQuantity, (bool?)(stock.AvailableQuantity >= rule.RequiredQuantity!.Value),
                        stock.AvailableQuantity >= rule.RequiredQuantity!.Value ? ResourceRequirementStatus.Sufficient : ResourceRequirementStatus.Insufficient)
                    : ((decimal?)null, (bool?)null, ResourceRequirementStatus.NotComparable)
                : ((decimal?)null, (bool?)null, ResourceRequirementStatus.Unknown);
            if (!Same(check.Requested, requested) || check.Sufficient != sufficient || check.RequirementStatus != status)
            {
                errors.Add(requested is null && check.Requested is not null
                    ? $"WeatherResource invented a requirement for resource {check.ResourceId}; no verified requested quantity was provided."
                    : $"WeatherResource requirement figures for resource {check.ResourceId} do not match the verified requirement and stock snapshot.");
            }
        }

        var assessments = output.ResourceRequirements ?? [];
        var expected = new Dictionary<Guid, ResourceRequirementAssessment>();
        foreach (var rule in rules)
        {
            var assessment = ExpectedAssessment(rule, stocksByResource, evidence.AvailabilityRequestedResourceIds);
            if (assessment is null) errors.Add($"WeatherResource did not check availability for required resource {rule.ResourceName}.");
            else expected[rule.RuleId] = assessment;
        }

        foreach (var rule in rules.Where(rule => rule.Status == RequirementCalculationStatus.Calculated))
        {
            if (assessments.Count(item => item.RuleId == rule.RuleId) != 1)
                errors.Add($"WeatherResource must report the verified requirement for {rule.ResourceName} exactly once.");
        }

        foreach (var assessment in assessments)
        {
            if (assessment.RuleId is null)
            {
                if (assessment.RequiredQuantity is not null || assessment.ShortageQuantity is not null || assessment.Sufficient is not null
                    || assessment.RequirementStatus != ResourceRequirementStatus.Unknown)
                    errors.Add($"WeatherResource invented a requirement for {assessment.ResourceName}; no verified rule supports it.");
                continue;
            }

            if (!expected.TryGetValue(assessment.RuleId.Value, out var want))
            {
                errors.Add($"WeatherResource referenced unknown requirement rule {assessment.RuleId}.");
                continue;
            }

            if (assessment.ResourceId != want.ResourceId
                || !Same(assessment.RequiredQuantity, want.RequiredQuantity)
                || !Same(assessment.AvailableQuantity, want.AvailableQuantity)
                || !Same(assessment.ReservedQuantity, want.ReservedQuantity)
                || !Same(assessment.ShortageQuantity, want.ShortageQuantity)
                || assessment.Sufficient != want.Sufficient
                || assessment.RequirementStatus != want.RequirementStatus)
                errors.Add($"WeatherResource requirement figures for {want.ResourceName} do not match the verified requirement, inventory and reservations.");
        }

        var expectedOverall = OverallStatus(evidence.Requirements, expected.Values.ToList());
        if (output.RequirementStatus != expectedOverall)
            errors.Add($"WeatherResource requirementStatus must be {expectedOverall}.");
        if (output.RequirementStatus != ResourceRequirementStatus.Sufficient && !output.RequiresHumanReview)
            errors.Add("WeatherResource must require human review when resource requirements are not confirmed sufficient.");

        return errors;
    }

    /// <summary>The single correct assessment of one verified rule; null when availability was never checked for it.</summary>
    public static ResourceRequirementAssessment? ExpectedAssessment(
        CalculatedResourceRequirement rule,
        IReadOnlyDictionary<Guid, StockSnapshot> stocksByResource,
        IReadOnlyCollection<Guid> checkedResourceIds)
    {
        ResourceRequirementAssessment Make(decimal? available, decimal? reserved, decimal? shortage, bool? sufficient, string status) =>
            new(rule.RuleId, rule.ResourceId, rule.ResourceName, rule.ResourceUnit, rule.RequiredQuantity, available, reserved, shortage, sufficient, status, rule.Basis, rule.Reason);

        if (rule.Status != RequirementCalculationStatus.Calculated || rule.RequiredQuantity is not { } required)
            return Make(null, null, null, null, ResourceRequirementStatus.Unknown);
        if (rule.ResourceMatch == ResourceMatchStatus.NotInCatalogue)
            return Make(0, 0, required, false, ResourceRequirementStatus.Insufficient);
        if (rule.ResourceMatch != ResourceMatchStatus.Matched || rule.ResourceId is not { } resourceId)
            return Make(null, null, null, null, ResourceRequirementStatus.NotComparable);

        if (!stocksByResource.TryGetValue(resourceId, out var stock))
        {
            return checkedResourceIds.Contains(resourceId)
                ? Make(0, 0, required, false, ResourceRequirementStatus.Insufficient)
                : null;
        }

        if (!UnitsMatch(rule.ResourceUnit, stock.Unit))
            return Make(stock.AvailableQuantity, stock.ReservedQuantity, null, null, ResourceRequirementStatus.NotComparable);

        var sufficient = stock.AvailableQuantity >= required;
        return Make(stock.AvailableQuantity, stock.ReservedQuantity, Math.Max(0, required - stock.AvailableQuantity), sufficient,
            sufficient ? ResourceRequirementStatus.Sufficient : ResourceRequirementStatus.Insufficient);
    }

    public static string OverallStatus(CropResourceRequirementsResult? requirements, IReadOnlyList<ResourceRequirementAssessment> assessments)
    {
        if (requirements is null || !requirements.Requirements.Any(rule => rule.Status == RequirementCalculationStatus.Calculated))
            return ResourceRequirementStatus.Unknown;
        if (assessments.Any(item => item.RequirementStatus == ResourceRequirementStatus.Insufficient))
            return ResourceRequirementStatus.Insufficient;
        return assessments.All(item => item.RequirementStatus == ResourceRequirementStatus.Sufficient)
            ? ResourceRequirementStatus.Sufficient
            : ResourceRequirementStatus.Incomplete;
    }

    private async Task<WeatherResourceToolEvidence> LoadToolEvidenceAsync(AgentStep step, Guid workflowId, CancellationToken cancellationToken)
    {
        var since = step.StartedAt ?? DateTime.MinValue;
        var executions = (await dbContext.AgentToolExecutions.AsNoTracking()
                .Where(item => item.AgentStepId == step.Id && !item.IsDeleted && item.Status == AgentToolExecutionStatus.Completed && item.CreatedAt >= since)
                .ToListAsync(cancellationToken))
            .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Where(item => ReadGuid(item.InputJson, "workflowId") == workflowId)
            .ToList();

        CropResourceRequirementsResult? requirements = null;
        WeatherForecastResponse? weather = null;
        var stocks = new List<StockSnapshot>();
        var requestedIds = new HashSet<Guid>();
        var availabilityRetrieved = false;
        foreach (var execution in executions)
        {
            switch (execution.ToolName)
            {
                case WeatherResourceToolNames.GetCropResourceRequirements:
                    requirements = Deserialize<CropResourceRequirementsResult>(execution.OutputJson) ?? requirements;
                    break;
                case WeatherResourceToolNames.GetResourceAvailability when Deserialize<List<StockSnapshot>>(execution.OutputJson) is { } rows:
                    availabilityRetrieved = true;
                    stocks.AddRange(rows);
                    requestedIds.UnionWith(ReadGuids(execution.InputJson, "resourceIds"));
                    break;
                case WeatherResourceToolNames.GetWeatherForecast:
                    weather = Deserialize<WeatherForecastResponse>(execution.OutputJson) ?? weather;
                    break;
            }
        }

        return new WeatherResourceToolEvidence(requirements, stocks, requestedIds, availabilityRetrieved, weather);
    }

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
        new(workflow.Id, workflow.CropPlanRequestId ?? Guid.Empty, step.Id, output.Status, output.WeatherRisk, output.RequiresHumanReview, output.Warnings, output.RequirementStatus);

    private static WeatherResourceOutput SafeFailure(Guid workflowId, IEnumerable<string> warnings) =>
        new(workflowId, "SafeFailure", true, warnings.ToArray(), "Unknown", string.Empty, [], [], [], ResourceRequirementStatus.Unknown);

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

    private static bool Same(decimal? left, decimal? right) =>
        left is null ? right is null : right is not null && Math.Abs(left.Value - right.Value) <= QuantityTolerance;

    private static bool UnitsMatch(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && string.Equals(left.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static T? Deserialize<T>(string json) where T : class
    {
        try { return JsonSerializer.Deserialize<T>(json, JsonOptions); }
        catch (JsonException) { return null; }
    }

    private static Guid? ReadGuid(string json, string property)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(property, out var value)
                && value.ValueKind == JsonValueKind.String
                && Guid.TryParse(value.GetString(), out var id) ? id : null;
        }
        catch (JsonException) { return null; }
    }

    private static IReadOnlyList<Guid> ReadGuids(string json, string property)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(property, out var values)
                || values.ValueKind != JsonValueKind.Array) return [];
            return values.EnumerateArray()
                .Select(value => value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToList();
        }
        catch (JsonException) { return []; }
    }
}
