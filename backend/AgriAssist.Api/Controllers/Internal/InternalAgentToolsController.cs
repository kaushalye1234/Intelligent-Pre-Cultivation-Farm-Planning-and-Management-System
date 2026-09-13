using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Models.Inspections;
using AgriAssist.Api.Models.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Controllers.Internal;

[ApiController]
[Route("api/internal/agent-tools")]
public sealed class InternalAgentToolsController(
    AppDbContext dbContext,
    IConfiguration configuration) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpGet("crop-plan-context/{cropPlanRequestId:guid}")]
    public async Task<ActionResult<AgentToolResponse<AgentCropPlanContextResponse>>> GetCropPlanContext(
        Guid cropPlanRequestId,
        [FromQuery] Guid? workflowId,
        [FromQuery] Guid? agentStepId,
        CancellationToken cancellationToken) =>
        await RunToolAsync(
            "GetCropPlanContext",
            workflowId,
            agentStepId,
            new { cropPlanRequestId, workflowId, agentStepId },
            async () =>
            {
                await EnsureWorkflowScopeAsync(workflowId, cropPlanRequestId, cancellationToken);
                var request = await dbContext.CropPlanRequests.AsNoTracking()
                    .Include(item => item.Farm)
                    .Include(item => item.Field)
                    .Include(item => item.CropType)
                    .SingleOrDefaultAsync(item => item.Id == cropPlanRequestId && !item.IsDeleted, cancellationToken)
                    ?? throw Safe(HttpStatusCode.NotFound, "Crop plan request was not found.");

                return new AgentCropPlanContextResponse(
                    request.Id,
                    request.FarmId,
                    request.FieldId,
                    request.CropTypeId,
                    request.RequestedByUserId,
                    request.PreferredStartDate,
                    request.PreferredEndDate,
                    request.Budget,
                    request.Objective,
                    request.Status.ToString(),
                    MapFarm(request.Farm!),
                    request.Field is null ? null : MapField(request.Field),
                    new AgentCropTypeDetailsResponse(request.CropType!.Id, request.CropType.Name, request.CropType.Description, request.CropType.IsActive));
            },
            cancellationToken);

    [HttpGet("farms/{farmId:guid}")]
    public async Task<ActionResult<AgentToolResponse<AgentFarmDetailsResponse>>> GetFarmDetails(
        Guid farmId,
        [FromQuery] Guid? workflowId,
        [FromQuery] Guid? agentStepId,
        CancellationToken cancellationToken) =>
        await RunToolAsync(
            "GetFarmDetails",
            workflowId,
            agentStepId,
            new { farmId, workflowId, agentStepId },
            async () =>
            {
                await EnsureFarmScopeAsync(workflowId, farmId, cancellationToken);
                var farm = await dbContext.Farms.AsNoTracking().SingleOrDefaultAsync(item => item.Id == farmId && !item.IsDeleted, cancellationToken)
                    ?? throw Safe(HttpStatusCode.NotFound, "Farm was not found.");
                return MapFarm(farm);
            },
            cancellationToken);

    [HttpGet("fields/{fieldId:guid}")]
    public async Task<ActionResult<AgentToolResponse<AgentFieldDetailsResponse>>> GetFieldDetails(
        Guid fieldId,
        [FromQuery] Guid? workflowId,
        [FromQuery] Guid? agentStepId,
        CancellationToken cancellationToken) =>
        await RunToolAsync(
            "GetFieldDetails",
            workflowId,
            agentStepId,
            new { fieldId, workflowId, agentStepId },
            async () =>
            {
                await EnsureFieldScopeAsync(workflowId, fieldId, cancellationToken);
                var field = await dbContext.Fields.AsNoTracking().SingleOrDefaultAsync(item => item.Id == fieldId && !item.IsDeleted, cancellationToken)
                    ?? throw Safe(HttpStatusCode.NotFound, "Field was not found.");
                return MapField(field);
            },
            cancellationToken);

    [HttpGet("crop-cycles/{cropCycleId:guid}")]
    public async Task<ActionResult<AgentToolResponse<AgentCropCycleDetailsResponse>>> GetCropCycleDetails(
        Guid cropCycleId,
        [FromQuery] Guid? workflowId,
        [FromQuery] Guid? agentStepId,
        CancellationToken cancellationToken) =>
        await RunToolAsync(
            "GetCropCycleDetails",
            workflowId,
            agentStepId,
            new { cropCycleId, workflowId, agentStepId },
            async () =>
            {
                await EnsureCropCycleScopeAsync(workflowId, cropCycleId, cancellationToken);
                var cycle = await dbContext.CropCycles.AsNoTracking().SingleOrDefaultAsync(item => item.Id == cropCycleId && !item.IsDeleted, cancellationToken)
                    ?? throw Safe(HttpStatusCode.NotFound, "Crop cycle was not found.");
                return new AgentCropCycleDetailsResponse(cycle.Id, cycle.FieldId, cycle.CropTypeId, cycle.PlannedStartDate, cycle.PlannedEndDate, cycle.Status.ToString());
            },
            cancellationToken);

    [HttpGet("crop-reference-profiles")]
    public async Task<ActionResult<AgentToolResponse<AgentCropReferenceProfileResponse>>> GetCropReferenceProfile(
        [FromQuery] Guid? cropTypeId,
        [FromQuery] Guid? cropReferenceProfileId,
        [FromQuery] string? varietyName,
        [FromQuery] string? region,
        [FromQuery] Guid? workflowId,
        [FromQuery] Guid? agentStepId,
        CancellationToken cancellationToken) =>
        await RunToolAsync(
            "GetCropReferenceProfile",
            workflowId,
            agentStepId,
            new { cropTypeId, cropReferenceProfileId, varietyName, region, workflowId, agentStepId },
            async () =>
            {
                if (!cropTypeId.HasValue && !cropReferenceProfileId.HasValue) throw Safe(HttpStatusCode.BadRequest, "Crop type or reference profile is required.");
                if (cropTypeId.HasValue) await EnsureCropTypeScopeAsync(workflowId, cropTypeId.Value, cancellationToken);
                if (cropReferenceProfileId.HasValue) await EnsureCropReferenceProfileScopeAsync(workflowId, cropReferenceProfileId.Value, cancellationToken);

                var profiles = dbContext.CropReferenceProfiles.AsNoTracking()
                    .Include(item => item.Stages)
                    .Include(item => item.Rules)
                    .Where(item => item.IsActive && !item.IsDeleted);

                if (cropReferenceProfileId.HasValue) profiles = profiles.Where(item => item.Id == cropReferenceProfileId.Value);
                if (cropTypeId.HasValue) profiles = profiles.Where(item => item.CropTypeId == cropTypeId.Value);
                if (!string.IsNullOrWhiteSpace(varietyName)) profiles = profiles.Where(item => item.VarietyName == varietyName);
                if (!string.IsNullOrWhiteSpace(region)) profiles = profiles.Where(item => item.Region == region);

                var profile = await profiles.OrderByDescending(item => item.VerifiedAt).FirstOrDefaultAsync(cancellationToken);

                if (profile is null)
                {
                    return new AgentCropReferenceProfileResponse(
                        "Unavailable",
                        null,
                        [],
                        [],
                        ["Verified crop reference data is missing."]);
                }

                return new AgentCropReferenceProfileResponse(
                    "Available",
                    new CropReferenceProfileSummary(profile.Id, profile.CropTypeId, profile.VarietyName, profile.Region, profile.SourceName, profile.SourceUrl, profile.SourceVersion, profile.VerifiedAt, profile.IsActive),
                    profile.Stages.OrderBy(stage => stage.Sequence).Select(stage => new CropStageReferenceSummary(stage.Id, stage.StageName, stage.Sequence, stage.TypicalMinDays, stage.TypicalMaxDays, stage.Notes, stage.SourceName, stage.SourceUrl)).ToArray(),
                    profile.Rules.OrderBy(rule => rule.RuleType).ThenBy(rule => rule.RuleKey).Select(rule => new CropRuleReferenceSummary(rule.Id, rule.RuleType, rule.RuleKey, rule.StructuredValueJson, rule.SourceName, rule.SourceUrl, rule.VerifiedAt)).ToArray(),
                    []);
            },
            cancellationToken);
    [HttpGet("crop-plan-history/{cropPlanRequestId:guid}")]
    public async Task<ActionResult<AgentToolResponse<List<CropPlanHistoryResponse>>>> GetRecentCropPlanHistory(
        Guid cropPlanRequestId,
        [FromQuery] Guid? workflowId,
        [FromQuery] Guid? agentStepId,
        CancellationToken cancellationToken) =>
        await RunToolAsync(
            "GetRecentCropPlanHistory",
            workflowId,
            agentStepId,
            new { cropPlanRequestId, workflowId, agentStepId },
            async () =>
            {
                await EnsureWorkflowScopeAsync(workflowId, cropPlanRequestId, cancellationToken);
                return await dbContext.CropPlanRequestHistories.AsNoTracking()
                    .Where(item => item.CropPlanRequestId == cropPlanRequestId && !item.IsDeleted)
                    .OrderByDescending(item => item.CreatedAt)
                    .Take(5)
                    .Select(item => new CropPlanHistoryResponse(item.Id, item.CropPlanRequestId, item.FromStatus, item.ToStatus, item.Note, item.ChangedByUserId, item.CreatedAt))
                    .ToListAsync(cancellationToken);
            },
            cancellationToken);


    [HttpGet("recent-inspections")]
    public async Task<ActionResult<AgentToolResponse<List<AgentInspectionSummaryResponse>>>> GetRecentInspections(
        [FromQuery] Guid fieldId,
        [FromQuery] Guid? workflowId,
        [FromQuery] Guid? agentStepId,
        CancellationToken cancellationToken) =>
        await RunToolAsync(
            "GetRecentInspections",
            workflowId,
            agentStepId,
            new { fieldId, workflowId, agentStepId },
            async () =>
            {
                if (fieldId == Guid.Empty) throw Safe(HttpStatusCode.BadRequest, "Field is required.");
                await EnsureFieldScopeAsync(workflowId, fieldId, cancellationToken);
                var inspections = await dbContext.FieldInspections.AsNoTracking()
                    .Where(item => item.FieldId == fieldId && !item.IsDeleted)
                    .OrderByDescending(item => item.CompletedAt ?? item.ScheduledAt)
                    .Take(8)
                    .ToListAsync(cancellationToken);
                var inspectionIds = inspections.Select(item => item.Id).ToArray();
                var observations = await dbContext.InspectionObservations.AsNoTracking()
                    .Where(item => inspectionIds.Contains(item.FieldInspectionId) && !item.IsDeleted)
                    .OrderBy(item => item.CreatedAt)
                    .Select(item => new { item.FieldInspectionId, Summary = new AgentInspectionObservationSummary(item.Id, item.ObservationType, item.Notes, item.CreatedAt) })
                    .ToListAsync(cancellationToken);

                return inspections.Select(inspection => new AgentInspectionSummaryResponse(
                    inspection.Id,
                    inspection.FieldId,
                    inspection.InspectorUserId,
                    inspection.ScheduledAt,
                    inspection.CompletedAt,
                    inspection.Status.ToString(),
                    inspection.Summary,
                    observations.Where(item => item.FieldInspectionId == inspection.Id).Select(item => item.Summary).ToArray())).ToList();
            },
            cancellationToken);

    [HttpGet("open-crop-issues")]
    public async Task<ActionResult<AgentToolResponse<List<AgentCropIssueSummaryResponse>>>> GetOpenCropIssues(
        [FromQuery] Guid fieldId,
        [FromQuery] Guid? workflowId,
        [FromQuery] Guid? agentStepId,
        CancellationToken cancellationToken) =>
        await RunToolAsync(
            "GetOpenCropIssues",
            workflowId,
            agentStepId,
            new { fieldId, workflowId, agentStepId },
            async () =>
            {
                if (fieldId == Guid.Empty) throw Safe(HttpStatusCode.BadRequest, "Field is required.");
                await EnsureFieldScopeAsync(workflowId, fieldId, cancellationToken);
                var issues = await dbContext.CropIssues.AsNoTracking()
                    .Include(item => item.FieldInspection)
                    .Where(item =>
                        item.FieldInspection != null &&
                        item.FieldInspection.FieldId == fieldId &&
                        !item.IsDeleted &&
                        (item.Status == CropIssueStatus.Open || item.Status == CropIssueStatus.Escalated))
                    .OrderByDescending(item => item.CreatedAt)
                    .ToListAsync(cancellationToken);
                var issueIds = issues.Select(item => item.Id).ToArray();
                var followUps = await dbContext.FollowUpRecommendations.AsNoTracking()
                    .Where(item => issueIds.Contains(item.CropIssueId) && !item.IsDeleted)
                    .OrderBy(item => item.DueAt ?? item.CreatedAt)
                    .Select(item => new { item.CropIssueId, Summary = new AgentFollowUpRecommendationSummary(item.Id, item.Recommendation, item.DueAt, item.IsCompleted) })
                    .ToListAsync(cancellationToken);

                return issues.Select(issue => new AgentCropIssueSummaryResponse(
                    issue.Id,
                    issue.FieldInspectionId,
                    issue.Title,
                    issue.Description,
                    issue.Severity.ToString(),
                    issue.Status.ToString(),
                    issue.CreatedAt,
                    issue.EscalatedAt,
                    followUps.Where(item => item.CropIssueId == issue.Id).Select(item => item.Summary).ToArray())).ToList();
            },
            cancellationToken);

    [HttpGet("inspection-image-metadata")]
    public async Task<ActionResult<AgentToolResponse<List<AgentInspectionImageMetadataResponse>>>> GetInspectionImageMetadata(
        [FromQuery] Guid fieldId,
        [FromQuery] Guid? workflowId,
        [FromQuery] Guid? agentStepId,
        CancellationToken cancellationToken) =>
        await RunToolAsync(
            "GetInspectionImageMetadata",
            workflowId,
            agentStepId,
            new { fieldId, workflowId, agentStepId },
            async () =>
            {
                if (fieldId == Guid.Empty) throw Safe(HttpStatusCode.BadRequest, "Field is required.");
                await EnsureFieldScopeAsync(workflowId, fieldId, cancellationToken);
                return await dbContext.InspectionImages.AsNoTracking()
                    .Include(item => item.FieldInspection)
                    .Where(item => item.FieldInspection != null && item.FieldInspection.FieldId == fieldId && !item.IsDeleted)
                    .OrderByDescending(item => item.CreatedAt)
                    .Select(item => new AgentInspectionImageMetadataResponse(item.Id, item.FieldInspectionId, item.Url, item.PublicId, item.ContentType, item.SizeBytes, item.CreatedAt))
                    .ToListAsync(cancellationToken);
            },
            cancellationToken);
    private async Task<ActionResult<AgentToolResponse<T>>> RunToolAsync<T>(
        string toolName,
        Guid? workflowId,
        Guid? agentStepId,
        object input,
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        if (!HasValidToolToken())
        {
            return Unauthorized(new AgentToolResponse<T>(workflowId, toolName, "Failed", default, "Internal tool token is invalid."));
        }

        var started = Stopwatch.GetTimestamp();
        var inputJson = JsonSerializer.Serialize(input, JsonOptions);
        try
        {
            var data = await action();
            await RecordToolExecutionAsync(workflowId, agentStepId, toolName, inputJson, data, AgentToolExecutionStatus.Completed, started, cancellationToken);
            return Ok(new AgentToolResponse<T>(workflowId, toolName, "Completed", data, null));
        }
        catch (InternalToolException exception)
        {
            var output = new { exception.SafeError };
            await RecordToolExecutionAsync(workflowId, agentStepId, toolName, inputJson, output, AgentToolExecutionStatus.Failed, started, cancellationToken);
            return StatusCode((int)exception.StatusCode, new AgentToolResponse<T>(workflowId, toolName, "Failed", default, exception.SafeError));
        }
    }

    private bool HasValidToolToken()
    {
        var expected = configuration["AI:ToolToken"];
        if (string.IsNullOrWhiteSpace(expected)) return false;

        var authorization = Request.Headers.Authorization.ToString();
        var provided = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization["Bearer ".Length..].Trim()
            : Request.Headers["X-AgriAssist-Tool-Token"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(provided)) return false;
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        return expectedBytes.Length == providedBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    private async Task RecordToolExecutionAsync<T>(
        Guid? workflowId,
        Guid? agentStepId,
        string toolName,
        string inputJson,
        T output,
        AgentToolExecutionStatus status,
        long started,
        CancellationToken cancellationToken)
    {
        var resolvedStepId = agentStepId;
        if (!resolvedStepId.HasValue && workflowId.HasValue)
        {
            resolvedStepId = await dbContext.AgentSteps
                .Where(step => step.AgentWorkflowId == workflowId.Value && (step.AgentName == "CropFieldAnalysisAgent" || step.AgentName == "CropPlanningCoordinatorAgent"))
                .OrderBy(step => step.AgentName == "CropFieldAnalysisAgent" ? 0 : 1)
                .ThenBy(step => step.Sequence)
                .Select(step => (Guid?)step.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (!resolvedStepId.HasValue) return;
        var stepExists = await dbContext.AgentSteps.AnyAsync(step => step.Id == resolvedStepId.Value, cancellationToken);
        if (!stepExists) return;

        dbContext.AgentToolExecutions.Add(new AgentToolExecution
        {
            AgentStepId = resolvedStepId.Value,
            ToolName = toolName,
            InputJson = inputJson,
            OutputJson = JsonSerializer.Serialize(output, JsonOptions),
            Status = status,
            DurationMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureWorkflowScopeAsync(Guid? workflowId, Guid cropPlanRequestId, CancellationToken cancellationToken)
    {
        if (!workflowId.HasValue) return;
        var allowed = await dbContext.AgentWorkflows.AsNoTracking()
            .AnyAsync(item => item.Id == workflowId.Value && item.CropPlanRequestId == cropPlanRequestId && !item.IsDeleted, cancellationToken);
        if (!allowed) throw Safe(HttpStatusCode.Forbidden, "Tool request does not match the workflow context.");
    }

    private async Task EnsureFarmScopeAsync(Guid? workflowId, Guid farmId, CancellationToken cancellationToken)
    {
        if (!workflowId.HasValue) return;
        var allowed = await dbContext.AgentWorkflows.AsNoTracking()
            .Include(item => item.CropPlanRequest)
            .AnyAsync(item => item.Id == workflowId.Value && item.CropPlanRequest != null && item.CropPlanRequest.FarmId == farmId, cancellationToken);
        if (!allowed) throw Safe(HttpStatusCode.Forbidden, "Farm tool request is outside the workflow context.");
    }

    private async Task EnsureFieldScopeAsync(Guid? workflowId, Guid fieldId, CancellationToken cancellationToken)
    {
        if (!workflowId.HasValue) return;
        var allowed = await dbContext.AgentWorkflows.AsNoTracking()
            .Include(item => item.CropPlanRequest)
            .AnyAsync(item => item.Id == workflowId.Value && item.CropPlanRequest != null && item.CropPlanRequest.FieldId == fieldId, cancellationToken);
        if (!allowed) throw Safe(HttpStatusCode.Forbidden, "Field tool request is outside the workflow context.");
    }

    private async Task EnsureCropCycleScopeAsync(Guid? workflowId, Guid cropCycleId, CancellationToken cancellationToken)
    {
        if (!workflowId.HasValue) return;
        var workflow = await dbContext.AgentWorkflows.AsNoTracking()
            .Include(item => item.CropPlanRequest)
            .SingleOrDefaultAsync(item => item.Id == workflowId.Value, cancellationToken);
        if (workflow?.CropPlanRequest is null) throw Safe(HttpStatusCode.Forbidden, "Workflow context was not found.");

        var allowed = await dbContext.CropCycles.AsNoTracking().AnyAsync(cycle =>
            cycle.Id == cropCycleId &&
            cycle.FieldId == workflow.CropPlanRequest.FieldId &&
            cycle.CropTypeId == workflow.CropPlanRequest.CropTypeId,
            cancellationToken);
        if (!allowed) throw Safe(HttpStatusCode.Forbidden, "Crop cycle tool request is outside the workflow context.");
    }

    private async Task EnsureCropTypeScopeAsync(Guid? workflowId, Guid cropTypeId, CancellationToken cancellationToken)
    {
        if (!workflowId.HasValue) return;
        var allowed = await dbContext.AgentWorkflows.AsNoTracking()
            .Include(item => item.CropPlanRequest)
            .AnyAsync(item => item.Id == workflowId.Value && item.CropPlanRequest != null && item.CropPlanRequest.CropTypeId == cropTypeId, cancellationToken);
        if (!allowed) throw Safe(HttpStatusCode.Forbidden, "Crop reference tool request is outside the workflow context.");
    }


    private async Task EnsureCropReferenceProfileScopeAsync(Guid? workflowId, Guid cropReferenceProfileId, CancellationToken cancellationToken)
    {
        if (!workflowId.HasValue) return;
        var workflow = await dbContext.AgentWorkflows.AsNoTracking()
            .Include(item => item.CropPlanRequest)
            .SingleOrDefaultAsync(item => item.Id == workflowId.Value, cancellationToken);
        if (workflow?.CropPlanRequest is null) throw Safe(HttpStatusCode.Forbidden, "Workflow context was not found.");

        var allowed = await dbContext.CropReferenceProfiles.AsNoTracking().AnyAsync(profile =>
            profile.Id == cropReferenceProfileId &&
            profile.CropTypeId == workflow.CropPlanRequest.CropTypeId &&
            profile.IsActive &&
            !profile.IsDeleted,
            cancellationToken);
        if (!allowed) throw Safe(HttpStatusCode.Forbidden, "Crop reference profile tool request is outside the workflow context.");
    }
    private static AgentFarmDetailsResponse MapFarm(AgriAssist.Api.Models.CropPlanning.Farm farm) =>
        new(farm.Id, farm.Name, farm.Location, farm.TotalArea, farm.OwnerUserId);

    private static AgentFieldDetailsResponse MapField(AgriAssist.Api.Models.CropPlanning.Field field) =>
        new(field.Id, field.FarmId, field.Name, field.Area, field.SoilType, field.IsActive);

    private static InternalToolException Safe(HttpStatusCode statusCode, string message) => new(statusCode, message);

    private sealed class InternalToolException(HttpStatusCode statusCode, string safeError) : Exception(safeError)
    {
        public HttpStatusCode StatusCode { get; } = statusCode;
        public string SafeError { get; } = safeError;
    }
}





