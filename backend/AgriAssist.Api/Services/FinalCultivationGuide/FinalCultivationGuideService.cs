using System.Text.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.FinalCultivationGuide;
using AgriAssist.Api.ExternalServices.AgenticAI;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Resources;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Models.TaskApproval;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Services.FinalCultivationGuide;

public sealed class FinalCultivationGuideService(
    AppDbContext dbContext,
    IFinalCultivationGuideAIClient aiClient,
    ILogger<FinalCultivationGuideService> logger) : IFinalCultivationGuideService
{
    private const string AgentName = "FinalCultivationGuideAgent";
    private const string StepName = "FarmerGuide";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<FinalCultivationGuideStatusDto> GenerateAsync(
        Guid workflowId,
        int approvedRevision,
        CancellationToken cancellationToken)
    {
        try
        {
            return await GenerateCoreAsync(workflowId, approvedRevision, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "Final cultivation guide could not be generated for workflow {WorkflowId}, revision {ApprovedRevision}; failure type {FailureType}.",
                workflowId, approvedRevision, exception.GetType().Name);
            return new FinalCultivationGuideStatusDto("Unavailable", approvedRevision, null);
        }
    }

    private async Task<FinalCultivationGuideStatusDto> GenerateCoreAsync(
        Guid workflowId,
        int approvedRevision,
        CancellationToken cancellationToken)
    {
        var workflow = await dbContext.AgentWorkflows
            .Include(item => item.CropPlanRequest)!.ThenInclude(item => item!.Farm)
            .Include(item => item.CropPlanRequest)!.ThenInclude(item => item!.Field)
            .Include(item => item.CropPlanRequest)!.ThenInclude(item => item!.CropType)
            .Include(item => item.CropPlanRequest)!.ThenInclude(item => item!.CropVariety)
            .Include(item => item.Steps)
            .SingleOrDefaultAsync(item => item.Id == workflowId && !item.IsDeleted, cancellationToken);

        if (workflow?.CropPlanRequest is not { Status: CropPlanRequestStatus.Approved } plan
            || workflow.Status != AgentWorkflowStatus.Completed
            || workflow.CandidateRevision != approvedRevision
            || !HasRequiredWorkflowEvidence(workflow.Steps, approvedRevision))
            return new FinalCultivationGuideStatusDto("Unavailable", approvedRevision, null);

        var approval = await dbContext.ApprovalDecisions.AsNoTracking()
            .Where(item => item.AgentWorkflowId == workflowId
                && item.CandidateRevision == approvedRevision
                && item.Decision == ApprovalDecisionType.Approved)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (approval is null)
            return new FinalCultivationGuideStatusDto("Unavailable", approvedRevision, null);

        var step = workflow.Steps.SingleOrDefault(item =>
            item.AgentName == AgentName && item.CandidateRevision == approvedRevision);
        if (step?.Status == AgentStepStatus.Completed)
        {
            var existing = DeserializeOutput(step.OutputJson);
            if (existing is not null)
                return new FinalCultivationGuideStatusDto("Ready", approvedRevision, existing);
        }
        if (step?.Status == AgentStepStatus.Running && step.StartedAt > DateTime.UtcNow.AddMinutes(-2))
            return new FinalCultivationGuideStatusDto("Pending", approvedRevision, null);

        var input = await BuildInputAsync(workflow, plan, approvedRevision, cancellationToken);
        if (step is null)
        {
            step = new AgentStep
            {
                AgentWorkflowId = workflowId,
                AgentName = AgentName,
                StepName = StepName,
                Sequence = workflow.Steps.Select(item => item.Sequence).DefaultIfEmpty().Max() + 1,
                CandidateRevision = approvedRevision,
                CreatedByUserId = approval.DecidedByUserId,
            };
            dbContext.AgentSteps.Add(step);
        }

        step.Status = AgentStepStatus.Running;
        step.StartedAt = DateTime.UtcNow;
        step.CompletedAt = null;
        step.RetryCount++;
        step.InputJson = JsonSerializer.Serialize(input, JsonOptions);
        step.OutputJson = "{}";
        step.ErrorCode = null;
        step.ErrorMessageSafe = null;
        step.UpdatedByUserId = approval.DecidedByUserId;
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var rawOutput = await aiClient.GenerateFinalCultivationGuideAsync(input, cancellationToken);
            var output = FinalCultivationGuideValidator.Validate(input, rawOutput);
            output = output with { ApprovedActivities = input.ApprovedActivities };
            step.OutputJson = JsonSerializer.Serialize(output, JsonOptions);
            step.Status = AgentStepStatus.Completed;
            step.CompletedAt = DateTime.UtcNow;
            step.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return new FinalCultivationGuideStatusDto("Ready", approvedRevision, output);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "Final cultivation guide generation failed for workflow {WorkflowId}, revision {ApprovedRevision}; failure type {FailureType}.",
                workflowId, approvedRevision, exception.GetType().Name);
            step.Status = AgentStepStatus.Failed;
            step.CompletedAt = DateTime.UtcNow;
            step.ErrorCode = "FINAL_GUIDE_UNAVAILABLE";
            step.ErrorMessageSafe = "Final guide temporarily unavailable.";
            step.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(CancellationToken.None);
            return new FinalCultivationGuideStatusDto("Unavailable", approvedRevision, null);
        }
    }

    private async Task<FinalCultivationGuideInputDto> BuildInputAsync(
        AgentWorkflow workflow,
        CropPlanRequest plan,
        int approvedRevision,
        CancellationToken cancellationToken)
    {
        var activities = new List<FinalGuideActivityDto>();
        activities.AddRange(await dbContext.FarmTasks.AsNoTracking()
            .Where(item => item.GeneratedByWorkflowId == workflow.Id
                && item.CandidateRevision == approvedRevision
                && item.Status == FarmTaskStatus.Approved
                && !item.IsDeleted)
            .Select(item => new FinalGuideActivityDto(item.Id, "FarmTask", item.Title, item.DueAt, null, null, null))
            .ToListAsync(cancellationToken));
        activities.AddRange(await dbContext.IrrigationSchedules.AsNoTracking()
            .Where(item => item.GeneratedByWorkflowId == workflow.Id
                && item.CandidateRevision == approvedRevision
                && item.Status == IrrigationScheduleStatus.Approved
                && !item.IsDeleted)
            .Select(item => new FinalGuideActivityDto(item.Id, "IrrigationSchedule", "Approved irrigation", item.ScheduledAt,
                null, null, item.DurationMinutes))
            .ToListAsync(cancellationToken));
        activities.AddRange(await dbContext.ResourceReservations.AsNoTracking()
            .Where(item => item.GeneratedByWorkflowId == workflow.Id
                && item.CandidateRevision == approvedRevision
                && item.Status == ResourceReservationStatus.Active
                && !item.IsDeleted)
            .Select(item => new FinalGuideActivityDto(item.Id, "ResourceReservation",
                item.InventoryStock != null && item.InventoryStock.Resource != null
                    ? item.InventoryStock.Resource.Name : "Reserved farm resource",
                null, item.Quantity,
                item.InventoryStock != null && item.InventoryStock.Resource != null
                    ? item.InventoryStock.Resource.Unit : null,
                null))
            .ToListAsync(cancellationToken));

        return new FinalCultivationGuideInputDto(
            1,
            workflow.Id,
            approvedRevision,
            plan.Id,
            plan.CropType?.Name ?? "Crop",
            plan.CropVariety?.Name,
            plan.Farm?.Name,
            plan.Field?.Name,
            plan.Farm?.Location,
            plan.PreferredStartDate,
            plan.PreferredEndDate,
            DateOnly.FromDateTime(DateTime.UtcNow),
            ReadEvidenceSummary(workflow.Steps),
            activities.OrderBy(item => item.ScheduledAt).ToList());
    }

    private static IReadOnlyList<string> ReadEvidenceSummary(IEnumerable<AgentStep> steps)
    {
        var allowedProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "objectiveSummary", "fieldSuitability", "soilAssessment", "waterAssessment", "drainageAssessment",
            "plantingReadiness", "weatherSummary", "weatherRisk", "priority", "recommendations", "warnings", "identifiedRisks"
        };
        var result = new List<string>();
        foreach (var step in steps.Where(item => item.Status == AgentStepStatus.Completed && item.AgentName != AgentName))
        {
            try
            {
                using var document = JsonDocument.Parse(step.OutputJson);
                foreach (var property in document.RootElement.EnumerateObject().Where(item => allowedProperties.Contains(item.Name)))
                {
                    if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        Add(property.Value.GetString());
                    }
                    else if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var value in property.Value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String))
                            Add(value.GetString());
                    }
                    else if (property.Value.ValueKind == JsonValueKind.Object
                        && property.Value.TryGetProperty("summary", out var summary)
                        && summary.ValueKind == JsonValueKind.String)
                    {
                        Add(summary.GetString());
                    }
                }
            }
            catch (JsonException)
            {
                continue;
            }
        }
        return result.Distinct(StringComparer.Ordinal).Take(40).ToList();

        void Add(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) result.Add(value.Trim()[..Math.Min(value.Trim().Length, 500)]);
        }
    }

    private static bool HasRequiredWorkflowEvidence(IEnumerable<AgentStep> steps, int approvedRevision)
    {
        var completed = steps.Where(item => item.Status == AgentStepStatus.Completed).ToArray();
        return completed.Any(item => item.AgentName == "CropPlanningCoordinatorAgent")
            && completed.Any(item => item.AgentName == "CropFieldAnalysisAgent")
            && completed.Any(item => item.AgentName == "WeatherResourceAgent")
            && completed.Any(item => item.AgentName == "SchedulingValidationAgent"
                && item.CandidateRevision == approvedRevision);
    }

    private static FinalCultivationGuideOutputDto? DeserializeOutput(string outputJson)
    {
        try
        {
            return JsonSerializer.Deserialize<FinalCultivationGuideOutputDto>(outputJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
