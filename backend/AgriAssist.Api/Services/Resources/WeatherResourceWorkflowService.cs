using System.Net;
using System.Text.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Inspections;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Dtos.Shared;
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
    Task<PagedResult<WeatherResourceWorkItemResponse>> GetWorkQueueAsync(PagedQuery query, CancellationToken cancellationToken);
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
    IWeatherResourceAIClient aiClient,
    ILogger<WeatherResourceWorkflowService> logger) : IWeatherResourceWorkflowService
{
    public const string AgentName = "WeatherResourceAgent";
    public const string StepName = "WeatherResourceAnalysis";
    public const string NextAgentName = "SchedulingValidationAgent";
    private const string FieldAnalysisAgentName = "CropFieldAnalysisAgent";
    private const string FieldAnalysisStepName = "FieldAnalysis";
    private const string OutputValidatorName = "WeatherResourceOutputValidator";
    private const string AvailabilityValidatorName = "WeatherResourceAvailability";
    private const decimal QuantityTolerance = 0.0005m;
    private const decimal WeatherTolerance = 0.01m;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] AllowedStatuses = ["Analyzed", "SafeFailure"];
    private static readonly string[] AllowedRisks = ["Low", "Medium", "High", "Unknown"];
    private static readonly string[] FactorLevels = ["Low", "Medium", "High"];

    /// <summary>Fixed weather-risk thresholds per measure; mirrors RISK_THRESHOLDS in the AI service.</summary>
    private static readonly Dictionary<string, (decimal Medium, decimal High)> WeatherThresholds = new(StringComparer.Ordinal)
    {
        ["DailyRainfall"] = (10m, 30m),
        ["TotalRainfall"] = (30m, 80m),
        ["MaxTemperature"] = (34m, 38m),
        ["MaxWind"] = (10m, 15m)
    };

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
        var farm = workflow.CropPlanRequest?.Farm;
        var location = WeatherLocationResolver.Resolve(farm?.Location, farm?.District);
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
                handoff.RequiresHumanReview,
                handoff.ReviewedCropIssueActions));

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
            validationErrors = Validate(output, evidence, workflow.Id, input);
            validatorName = OutputValidatorName;
            if (validationErrors.Count > 0) output = SafeFailure(workflow.Id, validationErrors);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // The user only sees the safe message below; the cause goes to the server log for diagnosis.
            logger.LogWarning(exception, "Weather and resource analysis failed for workflow {WorkflowId}.", workflow.Id);
            validationErrors = ["AI service is unavailable or timed out during weather and resource analysis. No assessment was generated."];
            validatorName = AvailabilityValidatorName;
            output = SafeFailure(workflow.Id, validationErrors);
        }

        var succeeded = !output.Status.Equals("SafeFailure", StringComparison.OrdinalIgnoreCase);
        step.OutputJson = JsonSerializer.Serialize(output, JsonOptions);
        step.Status = succeeded ? AgentStepStatus.Completed : AgentStepStatus.Failed;
        step.CompletedAt = DateTime.UtcNow;
        step.ErrorCode = succeeded ? null : validatorName == AvailabilityValidatorName ? "AI_SERVICE_UNAVAILABLE" : validationErrors.Count > 0 ? "AI_RESPONSE_INVALID" : "AI_SAFE_FAILURE";
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
    /// Read-only Resource Officer queue: crop plans whose latest workflow is waiting at WeatherResourceAgent.
    /// Membership is derived from the workflow alone (never from tasks, dashboard counts or client input), and
    /// a Running step stays listed so the UI can show it as in progress. Running the analysis still goes
    /// through RunAsync, which re-validates the current workflow.
    /// </summary>
    public async Task<PagedResult<WeatherResourceWorkItemResponse>> GetWorkQueueAsync(PagedQuery query, CancellationToken cancellationToken)
    {
        query.Normalize();
        var rows = dbContext.AgentWorkflows.AsNoTracking()
            .Where(workflow => !workflow.IsDeleted
                && workflow.CropPlanRequestId != null
                && workflow.CurrentStep == AgentName
                && workflow.CropPlanRequest != null
                && !workflow.CropPlanRequest.IsDeleted
                // Latest workflow for the request, using the same CreatedAt/Id convention as LoadWorkflowAsync.
                && workflow.Id == dbContext.AgentWorkflows
                    .Where(latest => latest.CropPlanRequestId == workflow.CropPlanRequestId && !latest.IsDeleted)
                    .OrderByDescending(latest => latest.CreatedAt)
                    .ThenByDescending(latest => latest.Id)
                    .Select(latest => latest.Id)
                    .FirstOrDefault())
            .Select(workflow => new
            {
                Workflow = workflow,
                Request = workflow.CropPlanRequest!,
                Step = workflow.Steps
                    .Where(step => step.AgentName == AgentName && step.StepName == StepName)
                    .OrderBy(step => step.Sequence)
                    .FirstOrDefault(),
                ReadyAt = workflow.Steps
                    .Where(step => step.AgentName == FieldAnalysisAgentName && step.StepName == FieldAnalysisStepName)
                    .OrderBy(step => step.Sequence)
                    .Select(step => step.CompletedAt)
                    .FirstOrDefault() ?? workflow.UpdatedAt
            })
            .Where(row => row.Step != null);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            rows = rows.Where(row =>
                row.Request.Objective.ToLower().Contains(search)
                || row.Request.Farm!.Name.ToLower().Contains(search)
                || row.Request.Farm!.Location.ToLower().Contains(search)
                || (row.Request.Field != null && row.Request.Field.Name.ToLower().Contains(search))
                || row.Request.CropType!.Name.ToLower().Contains(search)
                || (row.Request.CropVariety != null && row.Request.CropVariety.Name.ToLower().Contains(search)));
        }

        var descending = query.SortDirection == "desc";
        var sorted = query.SortBy?.Trim().ToLowerInvariant() switch
        {
            "preferredstartdate" => descending ? rows.OrderByDescending(row => row.Request.PreferredStartDate) : rows.OrderBy(row => row.Request.PreferredStartDate),
            "farmname" => descending ? rows.OrderByDescending(row => row.Request.Farm!.Name) : rows.OrderBy(row => row.Request.Farm!.Name),
            "cropname" => descending ? rows.OrderByDescending(row => row.Request.CropType!.Name) : rows.OrderBy(row => row.Request.CropType!.Name),
            _ => descending ? rows.OrderByDescending(row => row.ReadyAt) : rows.OrderBy(row => row.ReadyAt)
        };

        var totalCount = await rows.CountAsync(cancellationToken);
        var items = await sorted
            .ThenBy(row => row.Workflow.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(row => new WeatherResourceWorkItemResponse(
                row.Workflow.Id,
                row.Request.Id,
                row.Step!.Id,
                row.Request.Objective,
                row.Request.Farm!.Name,
                row.Request.Farm!.Location,
                row.Request.FieldId,
                row.Request.Field == null ? null : row.Request.Field.Name,
                row.Request.CropType!.Name,
                row.Request.CropVariety == null ? null : row.Request.CropVariety.Name,
                row.Request.PreferredStartDate,
                row.Request.PreferredEndDate,
                row.Workflow.CandidateRevision,
                row.Workflow.Version,
                row.Step!.Status,
                row.ReadyAt,
                row.Step!.StartedAt,
                row.Step!.ErrorCode,
                row.Step!.ErrorMessageSafe))
            .ToListAsync(cancellationToken);

        return new PagedResult<WeatherResourceWorkItemResponse>(items, query.Page, query.PageSize, totalCount);
    }

    /// <summary>
    /// The agent may only describe what the backend tools returned for this step: stock figures, verified
    /// requirement quantities and the forecast. Every derived figure (shortage, sufficiency, statuses) must follow.
    /// </summary>
    public static IReadOnlyList<string> Validate(
        WeatherResourceOutput output,
        WeatherResourceToolEvidence evidence,
        Guid workflowId,
        WeatherResourceInput? input = null)
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

        var considerations = output.CropHealthConsiderations ?? [];
        var knownActionKeys = (input?.Member2FieldAnalysisContext?.ReviewedCropIssueActions ?? [])
            .Select(action => action.ActionKey)
            .ToHashSet(StringComparer.Ordinal);
        var allowedConsiderationTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "WeatherTimingConstraint", "RainfallScheduling", "ResourceAvailability", "OperationalFeasibility"
        };
        if (considerations.Count > knownActionKeys.Count)
            errors.Add("Crop-health considerations exceed the supplied action count.");
        foreach (var consideration in considerations)
        {
            if (consideration.ContractVersion != Member2CropHealthContractVersions.Member3Consideration)
                errors.Add("A crop-health consideration contractVersion is unsupported.");
            if (!knownActionKeys.Contains(consideration.ActionKey))
                errors.Add("A crop-health consideration references an unknown Member 2 action.");
            if (!allowedConsiderationTypes.Contains(consideration.ConsiderationType))
                errors.Add("A crop-health consideration type is invalid.");
            if (string.IsNullOrWhiteSpace(consideration.Note) || consideration.Note.Length > 500)
                errors.Add("A crop-health consideration note is invalid.");
        }

        if (output.Status == "SafeFailure")
        {
            if (!output.RequiresHumanReview) errors.Add("WeatherResource safe failures must require human review.");
            return errors;
        }

        var weatherAvailable = evidence.Weather is { IsAvailable: true, Days.Count: > 0 };
        if (!weatherAvailable && output.WeatherRisk != "Unknown") errors.Add("WeatherResource reported a weather risk without forecast data.");
        if (output.WeatherRisk is "High" or "Unknown" && !output.RequiresHumanReview) errors.Add("WeatherResource High or Unknown weather risk must require human review.");
        errors.AddRange(ValidateRiskAssessment(output, evidence.Weather));

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

    /// <summary>
    /// The weather explanation may only restate the calculated risk: the same level, each forecast measure exactly once
    /// with the forecast's own value, peak day and the fixed thresholds, and no factors without a forecast. Its
    /// narrative is bounded here; the AI service already checks it against these facts.
    /// </summary>
    private static IEnumerable<string> ValidateRiskAssessment(WeatherResourceOutput output, WeatherForecastResponse? weather)
    {
        var assessment = output.WeatherRiskAssessment;
        if (assessment is null) yield break; // Older agents and stored outputs have no explanation.

        if (assessment.RiskLevel != output.WeatherRisk)
            yield return "WeatherResource weatherRiskAssessment riskLevel must equal weatherRisk.";
        if (assessment.GeneratedBy is not ("OpenAI" or "RuleBased"))
            yield return "WeatherResource weatherRiskAssessment generatedBy must be OpenAI or RuleBased.";
        var impacts = assessment.PotentialImpacts ?? [];
        var actions = assessment.RecommendedActions ?? [];
        if (!Bounded(assessment.Headline, 240) || !Bounded(assessment.Explanation, 1600) || (assessment.MonitoringAdvice?.Length ?? 0) > 800
            || impacts.Count > 6 || impacts.Any(impact => !Bounded(impact, 400))
            || actions.Count > 6 || actions.Any(action => action is null || !Bounded(action.Action, 400) || !Bounded(action.Timing, 160) || !FactorLevels.Contains(action.Priority)))
            yield return "WeatherResource weatherRiskAssessment text is missing, too long or has an invalid priority.";

        var factors = assessment.ContributingFactors ?? [];
        var days = weather is { IsAvailable: true, Days: not null } ? weather.Days : [];
        if (days.Count == 0)
        {
            if (factors.Count > 0) yield return "WeatherResource reported weather risk factors without forecast data.";
            yield break;
        }

        var expected = new Dictionary<string, (decimal Value, IReadOnlyList<DateOnly> PeakDays)>(StringComparer.Ordinal)
        {
            ["DailyRainfall"] = Peak(days, day => day.RainMm),
            ["TotalRainfall"] = (days.Sum(day => day.RainMm), []),
            ["MaxTemperature"] = Peak(days, day => day.MaxTemperatureC),
            ["MaxWind"] = Peak(days, day => day.MaxWindSpeedMs)
        };
        if (factors.Any(factor => factor is null || !expected.ContainsKey(factor.Metric))
            || factors.Count != expected.Count
            || factors.Select(factor => factor.Metric).Distinct().Count() != expected.Count)
        {
            yield return "WeatherResource must report each weather risk factor exactly once.";
            yield break;
        }

        foreach (var factor in factors)
        {
            var (value, peakDays) = expected[factor.Metric];
            var (medium, high) = WeatherThresholds[factor.Metric];
            var level = value >= high ? "High" : value >= medium ? "Medium" : "Low";
            var dayMatches = factor.Metric == "TotalRainfall"
                ? factor.ObservedOn is null
                : factor.ObservedOn is { } day && peakDays.Contains(day);
            if (Math.Abs(factor.Value - value) > WeatherTolerance || factor.Level != level
                || factor.MediumThreshold != medium || factor.HighThreshold != high || !dayMatches)
                yield return $"WeatherResource weather risk factor {factor.Metric} does not match the forecast and the fixed thresholds.";
        }

        var highest = FactorLevels[factors.Max(factor => Math.Max(0, Array.IndexOf(FactorLevels, factor.Level)))];
        if (output.WeatherRisk != highest)
            yield return "WeatherResource weatherRisk does not follow from the forecast thresholds.";
    }

    private static (decimal Value, IReadOnlyList<DateOnly> PeakDays) Peak(IReadOnlyList<WeatherDayResponse> days, Func<WeatherDayResponse, decimal> measure)
    {
        var peak = days.Max(measure);
        return (peak, days.Where(day => measure(day) == peak).Select(day => day.Date).ToList());
    }

    private static bool Bounded(string? text, int maxLength) => !string.IsNullOrWhiteSpace(text) && text.Length <= maxLength;

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
