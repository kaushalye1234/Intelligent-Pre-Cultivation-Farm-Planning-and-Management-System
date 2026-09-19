using System.Net;
using System.Text.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Dtos.TaskApproval;
using AgriAssist.Api.ExternalServices.AgenticAI;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Models.TaskApproval;
using AgriAssist.Api.Services.Resources;
using AgriAssist.Api.Services.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AgriAssist.Api.Services.TaskApproval;

public sealed class WorkflowApprovalService(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    ISchedulingValidationAIClient aiClient,
    IResourceService resourceService) : IWorkflowApprovalService
{
    private const string SchedulingAgentName = "SchedulingValidationAgent";
    private const string SchedulingStepName = "Scheduling";
    private const int MaxRevisionCount = 3;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PagedResult<WorkflowSummaryResponse>> SearchAsync(WorkflowApprovalQuery query, CancellationToken cancellationToken)
    {
        query.Normalize();
        if (query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
            throw BadRequest("WORKFLOW_FILTER_INVALID", "Workflow status filter is invalid.");

        var workflows = ApplyAccess(dbContext.AgentWorkflows.AsNoTracking()
            .Include(item => item.CropPlanRequest)!.ThenInclude(item => item!.Farm));
        if (query.Status.HasValue) workflows = workflows.Where(item => item.Status == query.Status.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            workflows = workflows.Where(item => item.Objective.ToLower().Contains(search));
        }

        var total = await workflows.CountAsync(cancellationToken);
        var items = await workflows
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(item => MapSummary(item))
            .ToListAsync(cancellationToken);
        return new PagedResult<WorkflowSummaryResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<WorkflowReviewResponse> GetAsync(Guid workflowId, CancellationToken cancellationToken)
    {
        var workflow = await LoadForReviewAsync(workflowId, asTracking: false, cancellationToken);
        return await MapReviewAsync(workflow, cancellationToken);
    }

    public async Task<WorkflowHistoryResponse> GetHistoryAsync(Guid workflowId, CancellationToken cancellationToken)
    {
        var review = await GetAsync(workflowId, cancellationToken);
        return new WorkflowHistoryResponse(workflowId, review.Steps, review.Validations, review.Decisions);
    }

    public async Task<WorkflowReviewResponse> GenerateCandidateAsync(Guid workflowId, CancellationToken cancellationToken)
    {
        RequireSchedulingOperator();
        var workflow = await LoadForReviewAsync(workflowId, asTracking: true, cancellationToken);
        var plan = workflow.CropPlanRequest ?? throw NotFound("Crop plan request");
        var step = FindSchedulingStep(workflow);

        if (workflow.Status == AgentWorkflowStatus.PendingOfficerApproval &&
            step.Status == AgentStepStatus.Completed &&
            step.CandidateRevision == workflow.CandidateRevision)
        {
            return await MapReviewAsync(workflow, cancellationToken);
        }
        if (step.Status == AgentStepStatus.Running)
            throw Conflict("SCHEDULING_ALREADY_RUNNING", "Scheduling validation is already running for this workflow.");
        if (workflow.Status is AgentWorkflowStatus.Completed or AgentWorkflowStatus.Rejected or AgentWorkflowStatus.Cancelled)
            throw Conflict("WORKFLOW_TERMINAL", "A completed, rejected, or cancelled workflow cannot generate another candidate.");

        var input = await BuildInputAsync(workflow, plan, cancellationToken);
        var actor = RequireUser();
        step.CandidateRevision = workflow.CandidateRevision;
        step.InputJson = JsonSerializer.Serialize(input, JsonOptions);
        step.OutputJson = "{}";
        step.Status = AgentStepStatus.Running;
        step.StartedAt = DateTime.UtcNow;
        step.CompletedAt = null;
        step.ErrorCode = null;
        step.ErrorMessageSafe = null;
        workflow.Status = AgentWorkflowStatus.Running;
        workflow.CurrentStep = SchedulingAgentName;
        workflow.Version++;
        MarkUpdated(workflow, actor);
        MarkUpdated(step, actor);
        await dbContext.SaveChangesAsync(cancellationToken);

        SchedulingValidationOutput output;
        try
        {
            output = await aiClient.RunSchedulingValidationAsync(input, cancellationToken);
        }
        catch (Exception)
        {
            output = SafeFailure(workflow.Id, workflow.CandidateRevision,
                ["Scheduling validation service is unavailable or timed out. No candidate work was created."]);
        }

        var (errors, warnings) = await ValidateCandidateAsync(workflow, output, cancellationToken);
        var ready = errors.Count == 0 && output.Status.Equals("CandidateReady", StringComparison.OrdinalIgnoreCase);
        step.OutputJson = JsonSerializer.Serialize(output, JsonOptions);
        step.CompletedAt = DateTime.UtcNow;
        step.Status = ready ? AgentStepStatus.Completed : AgentStepStatus.Failed;
        step.ErrorCode = ready ? null : output.Status.Equals("MissingDependency", StringComparison.OrdinalIgnoreCase)
            ? "MISSING_DEPENDENCY"
            : "CANDIDATE_VALIDATION_FAILED";
        step.ErrorMessageSafe = ready ? null : errors.Concat(output.Warnings ?? []).FirstOrDefault();
        MarkUpdated(step, actor);

        dbContext.AgentValidationResults.Add(new AgentValidationResult
        {
            AgentWorkflowId = workflow.Id,
            CandidateRevision = workflow.CandidateRevision,
            ValidatorName = "SchedulingCandidateValidator",
            IsValid = ready,
            ErrorsJson = JsonSerializer.Serialize(errors, JsonOptions),
            WarningsJson = JsonSerializer.Serialize(warnings, JsonOptions),
            CreatedByUserId = actor,
            UpdatedByUserId = actor
        });

        workflow.Status = ready ? AgentWorkflowStatus.PendingOfficerApproval
            : output.Status.Equals("MissingDependency", StringComparison.OrdinalIgnoreCase)
                ? AgentWorkflowStatus.MissingDependency
                : AgentWorkflowStatus.Failed;
        workflow.CurrentStep = ready ? "HumanApproval" : step.ErrorCode ?? "SchedulingValidationFailed";
        workflow.CompletedAt = ready ? null : DateTime.UtcNow;
        workflow.Version++;
        MarkUpdated(workflow, actor);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapReviewAsync(workflow, cancellationToken);
    }

    public Task<WorkflowDecisionResponse> ApproveAsync(Guid workflowId, WorkflowDecisionRequest request, CancellationToken cancellationToken) =>
        DecideAsync(workflowId, request, ApprovalDecisionType.Approved, cancellationToken);

    public Task<WorkflowDecisionResponse> RejectAsync(Guid workflowId, WorkflowDecisionRequest request, CancellationToken cancellationToken) =>
        DecideAsync(workflowId, request, ApprovalDecisionType.Rejected, cancellationToken);

    public Task<WorkflowDecisionResponse> RequestRevisionAsync(Guid workflowId, WorkflowDecisionRequest request, CancellationToken cancellationToken) =>
        DecideAsync(workflowId, request, ApprovalDecisionType.RevisionRequested, cancellationToken);

    private async Task<WorkflowDecisionResponse> DecideAsync(
        Guid workflowId,
        WorkflowDecisionRequest request,
        ApprovalDecisionType decisionType,
        CancellationToken cancellationToken)
    {
        RequireApprover();
        ValidateDecisionRequest(request, decisionType);
        var replay = await FindReplayAsync(workflowId, request, decisionType, cancellationToken);
        if (replay is not null) return replay;

        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            var workflow = await LoadForReviewAsync(workflowId, asTracking: true, cancellationToken);
            var plan = workflow.CropPlanRequest ?? throw NotFound("Crop plan request");
            if (workflow.CandidateRevision != request.CandidateRevision || workflow.Version != request.ExpectedWorkflowVersion)
                throw Conflict("WORKFLOW_STALE", "The workflow changed after it was reviewed. Refresh before deciding.");
            if (workflow.Status != AgentWorkflowStatus.PendingOfficerApproval)
                throw Conflict("WORKFLOW_DECISION_NOT_ALLOWED", "Only workflows pending officer approval can receive a decision.");

            var actor = RequireUser();
            var taskIds = new List<Guid>();
            var scheduleIds = new List<Guid>();
            var reservationIds = new List<Guid>();

            if (decisionType == ApprovalDecisionType.Approved)
            {
                var step = FindSchedulingStep(workflow);
                var output = ReadSchedulingOutput(step.OutputJson, workflow.Id, workflow.CandidateRevision);
                var (errors, _) = await ValidateCandidateAsync(workflow, output, cancellationToken);
                if (errors.Count > 0)
                    throw Conflict("WORKFLOW_REVALIDATION_FAILED", string.Join(" ", errors));

                foreach (var candidate in output.CandidateTasks)
                {
                    var task = new FarmTask
                    {
                        FarmId = candidate.FarmId,
                        Title = candidate.Title.Trim(),
                        Description = candidate.Description.Trim(),
                        DueAt = candidate.DueAt,
                        AssignedToUserId = candidate.AssignedToUserId,
                        Status = FarmTaskStatus.Approved,
                        GeneratedByWorkflowId = workflow.Id,
                        CandidateRevision = workflow.CandidateRevision,
                        CreatedByUserId = actor,
                        UpdatedByUserId = actor
                    };
                    dbContext.FarmTasks.Add(task);
                    taskIds.Add(task.Id);
                }

                foreach (var candidate in output.CandidateIrrigation)
                {
                    var schedule = new IrrigationSchedule
                    {
                        FieldId = candidate.FieldId,
                        ScheduledAt = candidate.ScheduledAt,
                        DurationMinutes = candidate.DurationMinutes,
                        Notes = candidate.Notes.Trim(),
                        Status = IrrigationScheduleStatus.Approved,
                        GeneratedByWorkflowId = workflow.Id,
                        CandidateRevision = workflow.CandidateRevision,
                        CreatedByUserId = actor,
                        UpdatedByUserId = actor
                    };
                    dbContext.IrrigationSchedules.Add(schedule);
                    scheduleIds.Add(schedule.Id);
                }

                foreach (var candidate in output.CandidateReservations)
                {
                    var reservation = await resourceService.StageWorkflowReservationAsync(
                        new ResourceReservationRequest(candidate.InventoryStockId, candidate.Quantity, candidate.Purpose),
                        workflow.Id,
                        workflow.CandidateRevision,
                        plan.RequestedByUserId,
                        cancellationToken);
                    reservationIds.Add(reservation.Id);
                }

                var previousStatus = plan.Status;
                plan.Status = CropPlanRequestStatus.Approved;
                MarkUpdated(plan, actor);
                dbContext.CropPlanRequestHistories.Add(new CropPlanRequestHistory
                {
                    CropPlanRequestId = plan.Id,
                    FromStatus = previousStatus,
                    ToStatus = plan.Status,
                    Note = $"Workflow candidate revision {workflow.CandidateRevision} approved.",
                    ChangedByUserId = actor,
                    CreatedByUserId = actor,
                    UpdatedByUserId = actor
                });
                workflow.Status = AgentWorkflowStatus.Completed;
                workflow.CurrentStep = "Completed";
                workflow.CompletedAt = DateTime.UtcNow;
            }
            else if (decisionType == ApprovalDecisionType.Rejected)
            {
                var previousStatus = plan.Status;
                plan.Status = CropPlanRequestStatus.Rejected;
                MarkUpdated(plan, actor);
                dbContext.CropPlanRequestHistories.Add(new CropPlanRequestHistory
                {
                    CropPlanRequestId = plan.Id,
                    FromStatus = previousStatus,
                    ToStatus = plan.Status,
                    Note = request.Comment.Trim(),
                    ChangedByUserId = actor,
                    CreatedByUserId = actor,
                    UpdatedByUserId = actor
                });
                workflow.Status = AgentWorkflowStatus.Rejected;
                workflow.CurrentStep = "Rejected";
                workflow.CompletedAt = DateTime.UtcNow;
            }
            else
            {
                if (workflow.RevisionCount >= MaxRevisionCount)
                    throw Conflict("REVISION_LIMIT_REACHED", $"A workflow can be revised at most {MaxRevisionCount} times.");
                workflow.RevisionCount++;
                workflow.CandidateRevision++;
                workflow.Status = AgentWorkflowStatus.RevisionRequested;
                workflow.CurrentStep = SchedulingAgentName;
                workflow.CompletedAt = null;
                var step = FindSchedulingStep(workflow);
                step.CandidateRevision = workflow.CandidateRevision;
                step.Status = AgentStepStatus.Pending;
                step.InputJson = "{}";
                step.OutputJson = "{}";
                step.StartedAt = null;
                step.CompletedAt = null;
                step.ErrorCode = null;
                step.ErrorMessageSafe = null;
                MarkUpdated(step, actor);
            }

            var decision = new ApprovalDecision
            {
                AgentWorkflowId = workflow.Id,
                CandidateRevision = request.CandidateRevision,
                ExpectedWorkflowVersion = request.ExpectedWorkflowVersion,
                IdempotencyKey = request.IdempotencyKey.Trim(),
                DecidedByUserId = actor,
                Decision = decisionType,
                Comment = request.Comment.Trim(),
                CreatedByUserId = actor,
                UpdatedByUserId = actor
            };
            dbContext.ApprovalDecisions.Add(decision);
            workflow.Version++;
            MarkUpdated(workflow, actor);
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return new WorkflowDecisionResponse(workflow.Id, workflow.Status, workflow.CandidateRevision, workflow.Version,
                decisionType, decision.Id, taskIds, scheduleIds, reservationIds);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            throw Conflict("WORKFLOW_CHANGED", "Another officer decided or changed this workflow. Refresh before retrying.");
        }
        catch (DbUpdateException)
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            throw Conflict("WORKFLOW_DECISION_CONFLICT", "The workflow decision conflicted with another database change. Refresh before retrying.");
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<SchedulingValidationInput> BuildInputAsync(
        AgentWorkflow workflow,
        CropPlanRequest plan,
        CancellationToken cancellationToken)
    {
        JsonElement StepOutput(string name) => ParseJson(workflow.Steps
            .OrderBy(item => item.Sequence)
            .LastOrDefault(item => item.AgentName == name && item.Status == AgentStepStatus.Completed)?.OutputJson);

        var tasks = await dbContext.FarmTasks.AsNoTracking()
            .Where(item => !item.IsDeleted && item.FarmId == plan.FarmId)
            .OrderByDescending(item => item.CreatedAt).Take(500)
            .Select(item => new ExistingFarmTaskSnapshot(item.Id, item.AssignedToUserId, item.DueAt, item.Status))
            .ToListAsync(cancellationToken);
        var irrigation = plan.FieldId.HasValue
            ? await dbContext.IrrigationSchedules.AsNoTracking()
                .Where(item => !item.IsDeleted && item.FieldId == plan.FieldId.Value)
                .OrderByDescending(item => item.CreatedAt).Take(500)
                .Select(item => new ExistingIrrigationSnapshot(item.Id, item.FieldId, item.ScheduledAt, item.DurationMinutes, item.Status))
                .ToListAsync(cancellationToken)
            : [];

        return new SchedulingValidationInput(
            workflow.Id,
            workflow.CandidateRevision,
            plan.Id,
            plan.FarmId,
            plan.FieldId,
            plan.RequestedByUserId,
            plan.PreferredStartDate,
            plan.PreferredEndDate,
            plan.Budget,
            StepOutput("CropPlanningCoordinatorAgent"),
            StepOutput("CropFieldAnalysisAgent"),
            StepOutput("WeatherResourceAgent"),
            tasks,
            irrigation);
    }

    private async Task<(List<string> Errors, List<string> Warnings)> ValidateCandidateAsync(
        AgentWorkflow workflow,
        SchedulingValidationOutput output,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var warnings = (output.Warnings ?? []).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct().ToList();
        var plan = workflow.CropPlanRequest ?? throw NotFound("Crop plan request");
        if (output.WorkflowId != workflow.Id) errors.Add("Candidate workflowId does not match the persisted workflow.");
        if (output.CandidateRevision != workflow.CandidateRevision) errors.Add("Candidate revision does not match the current workflow revision.");
        if (!output.Status.Equals("CandidateReady", StringComparison.OrdinalIgnoreCase))
            errors.Add("Candidate status must be CandidateReady before officer approval.");
        if (!output.RequiresHumanApproval) errors.Add("A ready scheduling candidate must require human approval.");
        if (output.CandidateTasks is null || output.CandidateTasks.Count is < 1 or > 50)
            errors.Add("Candidate must contain between 1 and 50 tasks.");
        if (output.CandidateIrrigation is null || output.CandidateIrrigation.Count is < 1 or > 20)
            errors.Add("Candidate must contain between 1 and 20 irrigation schedules.");
        if (output.CandidateReservations is null || output.CandidateReservations.Count > 50)
            errors.Add("Candidate may contain at most 50 resource reservations.");
        if (output.Constraints is null || output.Constraints.Any(item => string.IsNullOrWhiteSpace(item.Code) || string.IsNullOrWhiteSpace(item.Message)))
            errors.Add("Candidate constraints must have codes and messages.");
        if (errors.Count > 0 && (output.CandidateTasks is null || output.CandidateIrrigation is null || output.CandidateReservations is null))
            return (errors, warnings);

        var start = DateTime.SpecifyKind(plan.PreferredStartDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(plan.PreferredEndDate.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);
        var activeUserIds = await dbContext.Users.AsNoTracking()
            .Where(item => item.IsActive && !item.IsDeleted)
            .Select(item => item.Id).ToListAsync(cancellationToken);
        var existingTasks = await dbContext.FarmTasks.AsNoTracking()
            .Where(item => !item.IsDeleted && item.Status != FarmTaskStatus.Rejected && item.Status != FarmTaskStatus.Completed && item.Status != FarmTaskStatus.Cancelled)
            .Select(item => new { item.AssignedToUserId, item.DueAt }).ToListAsync(cancellationToken);

        var candidateTaskSlots = new HashSet<(Guid, DateTime)>();
        foreach (var task in output.CandidateTasks ?? [])
        {
            if (task.FarmId != plan.FarmId) errors.Add("Candidate task references a farm outside the workflow.");
            if (!activeUserIds.Contains(task.AssignedToUserId)) errors.Add("Candidate task references an inactive or unknown assignee.");
            if (string.IsNullOrWhiteSpace(task.Title) || task.Title.Length > 160) errors.Add("Candidate task title is invalid.");
            if ((task.Description?.Length ?? 0) > 1500) errors.Add("Candidate task description is too long.");
            if (task.DueAt < start || task.DueAt > end || task.DueAt <= DateTime.UtcNow) errors.Add("Candidate task falls outside the valid future date window.");
            if (!candidateTaskSlots.Add((task.AssignedToUserId, task.DueAt)) || existingTasks.Any(item => item.AssignedToUserId == task.AssignedToUserId && item.DueAt == task.DueAt))
                errors.Add("Candidate task conflicts with another task for the assignee.");
        }

        var existingSchedules = await dbContext.IrrigationSchedules.AsNoTracking()
            .Where(item => !item.IsDeleted && item.Status != IrrigationScheduleStatus.Rejected && item.Status != IrrigationScheduleStatus.Completed && item.Status != IrrigationScheduleStatus.Cancelled)
            .Select(item => new { item.FieldId, item.ScheduledAt, item.DurationMinutes }).ToListAsync(cancellationToken);
        var candidateSchedules = new List<SchedulingCandidateIrrigation>();
        foreach (var schedule in output.CandidateIrrigation ?? [])
        {
            if (!plan.FieldId.HasValue || schedule.FieldId != plan.FieldId.Value) errors.Add("Candidate irrigation references a field outside the workflow.");
            if (schedule.DurationMinutes is < 1 or > 1440) errors.Add("Candidate irrigation duration is invalid.");
            if ((schedule.Notes?.Length ?? 0) > 1000) errors.Add("Candidate irrigation notes are too long.");
            var scheduleEnd = schedule.ScheduledAt.AddMinutes(schedule.DurationMinutes);
            if (schedule.ScheduledAt < start || scheduleEnd > end || schedule.ScheduledAt <= DateTime.UtcNow) errors.Add("Candidate irrigation falls outside the valid future date window.");
            if (existingSchedules.Any(item => item.FieldId == schedule.FieldId && item.ScheduledAt < scheduleEnd && item.ScheduledAt.AddMinutes(item.DurationMinutes) > schedule.ScheduledAt) ||
                candidateSchedules.Any(item => item.FieldId == schedule.FieldId && item.ScheduledAt < scheduleEnd && item.ScheduledAt.AddMinutes(item.DurationMinutes) > schedule.ScheduledAt))
                errors.Add("Candidate irrigation conflicts with an existing or proposed schedule.");
            candidateSchedules.Add(schedule);
        }

        var reservations = output.CandidateReservations ?? [];
        var stockIds = reservations.Select(item => item.InventoryStockId).Distinct().ToList();
        var stocks = await dbContext.InventoryStocks.AsNoTracking()
            .Where(item => stockIds.Contains(item.Id) && !item.IsDeleted)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        foreach (var reservation in reservations)
        {
            if (reservation.Quantity <= 0) errors.Add("Candidate reservation quantity must be positive.");
            if (string.IsNullOrWhiteSpace(reservation.Purpose) || reservation.Purpose.Length > 500) errors.Add("Candidate reservation purpose is invalid.");
            if (!stocks.TryGetValue(reservation.InventoryStockId, out var stock)) errors.Add("Candidate reservation references unknown inventory.");
            else if (stock.AvailableQuantity < reservation.Quantity) errors.Add("Candidate reservation exceeds current available stock.");
        }
        foreach (var group in reservations.GroupBy(item => item.InventoryStockId))
        {
            if (stocks.TryGetValue(group.Key, out var stock) && group.Sum(item => item.Quantity) > stock.AvailableQuantity)
                errors.Add("Combined candidate reservations exceed current available stock.");
        }

        decimal? computedCost = reservations.Count == 0 || reservations.Any(item => !item.EstimatedUnitCost.HasValue)
            ? null
            : reservations.Sum(item => item.Quantity * item.EstimatedUnitCost!.Value);
        if (computedCost.HasValue && output.EstimatedCost != computedCost) errors.Add("Candidate estimated cost does not match its resource quantities.");
        if (!computedCost.HasValue && output.EstimatedCost.HasValue) errors.Add("Candidate estimated cost is unsupported without authoritative unit costs.");
        if (computedCost > plan.Budget) errors.Add("Candidate estimated cost exceeds the crop plan budget.");
        if (!computedCost.HasValue) warnings.Add("Authoritative cost data is incomplete; no cost was assumed.");

        return (errors.Distinct().ToList(), warnings.Distinct().ToList());
    }

    private async Task<WorkflowDecisionResponse?> FindReplayAsync(
        Guid workflowId,
        WorkflowDecisionRequest request,
        ApprovalDecisionType decisionType,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.ApprovalDecisions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.AgentWorkflowId == workflowId && item.IdempotencyKey == request.IdempotencyKey.Trim(), cancellationToken);
        if (existing is null) return null;
        if (existing.Decision != decisionType || existing.CandidateRevision != request.CandidateRevision)
            throw Conflict("IDEMPOTENCY_KEY_REUSED", "The idempotency key was already used for a different workflow decision.");

        var workflow = await ApplyAccess(dbContext.AgentWorkflows.AsNoTracking()
            .Include(item => item.CropPlanRequest)!.ThenInclude(item => item!.Farm))
            .SingleOrDefaultAsync(item => item.Id == workflowId, cancellationToken) ?? throw NotFound("AI workflow");
        var tasks = await dbContext.FarmTasks.AsNoTracking().Where(item => item.GeneratedByWorkflowId == workflowId && item.CandidateRevision == existing.CandidateRevision).Select(item => item.Id).ToListAsync(cancellationToken);
        var schedules = await dbContext.IrrigationSchedules.AsNoTracking().Where(item => item.GeneratedByWorkflowId == workflowId && item.CandidateRevision == existing.CandidateRevision).Select(item => item.Id).ToListAsync(cancellationToken);
        var reservations = await dbContext.ResourceReservations.AsNoTracking().Where(item => item.GeneratedByWorkflowId == workflowId && item.CandidateRevision == existing.CandidateRevision).Select(item => item.Id).ToListAsync(cancellationToken);
        return new WorkflowDecisionResponse(workflowId, workflow.Status, workflow.CandidateRevision, workflow.Version, existing.Decision, existing.Id, tasks, schedules, reservations);
    }

    private async Task<AgentWorkflow> LoadForReviewAsync(Guid workflowId, bool asTracking, CancellationToken cancellationToken)
    {
        IQueryable<AgentWorkflow> query = dbContext.AgentWorkflows
            .Include(item => item.CropPlanRequest)!.ThenInclude(item => item!.Farm)
            .Include(item => item.Steps)
            .Include(item => item.ValidationResults)
            .Where(item => !item.IsDeleted);
        if (!asTracking) query = query.AsNoTracking();
        return await ApplyAccess(query).SingleOrDefaultAsync(item => item.Id == workflowId, cancellationToken)
            ?? throw NotFound("AI workflow");
    }

    private IQueryable<AgentWorkflow> ApplyAccess(IQueryable<AgentWorkflow> query)
    {
        if (currentUser.Role != ApplicationRole.Farmer) return query;
        var userId = RequireUser();
        return query.Where(item => item.CropPlanRequest != null &&
            (item.CropPlanRequest.RequestedByUserId == userId || item.CropPlanRequest.Farm!.OwnerUserId == userId));
    }

    private async Task<WorkflowReviewResponse> MapReviewAsync(AgentWorkflow workflow, CancellationToken cancellationToken)
    {
        var plan = workflow.CropPlanRequest ?? throw NotFound("Crop plan request");
        var decisions = await dbContext.ApprovalDecisions.AsNoTracking()
            .Where(item => item.AgentWorkflowId == workflow.Id && !item.IsDeleted)
            .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(item => new ApprovalDecisionResponse(item.Id, item.FarmTaskId, item.IrrigationScheduleId, item.DecidedByUserId,
                item.AgentWorkflowId, item.Decision, item.Comment, item.CreatedAt))
            .ToListAsync(cancellationToken);
        var steps = workflow.Steps.OrderBy(item => item.Sequence).ThenBy(item => item.Id)
            .Select(item => new WorkflowStepReviewResponse(item.Id, item.AgentName, item.StepName, item.Sequence,
                item.CandidateRevision, item.Status, ParseJson(item.InputJson), ParseJson(item.OutputJson), item.StartedAt,
                item.CompletedAt, item.ErrorCode, item.ErrorMessageSafe)).ToList();
        var validations = workflow.ValidationResults.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(item => new WorkflowValidationResponse(item.Id, item.ValidatorName, item.CandidateRevision, item.IsValid,
                ReadStringList(item.ErrorsJson), ReadStringList(item.WarningsJson), item.CreatedAt)).ToList();
        return new WorkflowReviewResponse(MapSummary(workflow), plan.FarmId, plan.FieldId, plan.Budget,
            plan.PreferredStartDate, plan.PreferredEndDate, steps, validations, decisions);
    }

    private static WorkflowSummaryResponse MapSummary(AgentWorkflow workflow) =>
        new(workflow.Id, workflow.CropPlanRequestId, workflow.Objective, workflow.Status, workflow.CurrentStep,
            workflow.CandidateRevision, workflow.RevisionCount, workflow.Version, workflow.CreatedAt, workflow.CompletedAt);

    private static AgentStep FindSchedulingStep(AgentWorkflow workflow) =>
        workflow.Steps.OrderBy(item => item.Sequence)
            .LastOrDefault(item => item.AgentName == SchedulingAgentName && item.StepName == SchedulingStepName)
        ?? throw NotFound("Scheduling validation step");

    private static SchedulingValidationOutput ReadSchedulingOutput(string json, Guid workflowId, int revision)
    {
        try
        {
            return JsonSerializer.Deserialize<SchedulingValidationOutput>(json, JsonOptions)
                ?? SafeFailure(workflowId, revision, ["Scheduling candidate output is empty."]);
        }
        catch (JsonException)
        {
            return SafeFailure(workflowId, revision, ["Scheduling candidate output could not be read safely."]);
        }
    }

    private static SchedulingValidationOutput SafeFailure(Guid workflowId, int revision, IReadOnlyList<string> warnings) =>
        new(workflowId, revision, "SafeFailure", true, false, warnings, [], [], [], null, []);

    private static JsonElement ParseJson(string? json)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            using var document = JsonDocument.Parse("{}");
            return document.RootElement.Clone();
        }
    }

    private static IReadOnlyList<string> ReadStringList(string? json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json ?? "[]", JsonOptions) ?? []; }
        catch (JsonException) { return ["Stored validation evidence could not be read safely."]; }
    }

    private static void ValidateDecisionRequest(WorkflowDecisionRequest request, ApprovalDecisionType decision)
    {
        if (request.CandidateRevision < 1 || request.ExpectedWorkflowVersion < 1)
            throw BadRequest("WORKFLOW_VERSION_REQUIRED", "Candidate revision and expected workflow version are required.");
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Trim().Length > 120)
            throw BadRequest("IDEMPOTENCY_KEY_INVALID", "An idempotency key of 120 characters or fewer is required.");
        if ((request.Comment?.Length ?? 0) > 1000)
            throw BadRequest("DECISION_COMMENT_INVALID", "Decision comment must be 1000 characters or fewer.");
        if (decision is ApprovalDecisionType.Rejected or ApprovalDecisionType.RevisionRequested && string.IsNullOrWhiteSpace(request.Comment))
            throw BadRequest("DECISION_COMMENT_REQUIRED", "A reason is required when rejecting or requesting revision.");
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) =>
        dbContext.Database.IsRelational() ? await dbContext.Database.BeginTransactionAsync(cancellationToken) : null;

    private Guid RequireUser() => currentUser.UserId
        ?? throw new ApiException(HttpStatusCode.Unauthorized, "AUTH_REQUIRED", "Authentication is required.");

    private void RequireSchedulingOperator()
    {
        if (currentUser.Role is not (ApplicationRole.AgriculturalOfficer or ApplicationRole.Admin))
            throw new ApiException(HttpStatusCode.Forbidden, "SCHEDULING_ROLE_REQUIRED", "An agricultural officer or administrator is required.");
    }

    private void RequireApprover()
    {
        if (currentUser.Role is not (ApplicationRole.AgriculturalOfficer or ApplicationRole.Admin))
            throw new ApiException(HttpStatusCode.Forbidden, "APPROVER_REQUIRED", "An agricultural officer or administrator is required.");
    }

    private static void MarkUpdated(AuditableEntity entity, Guid actor)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = actor;
    }

    private static ApiException NotFound(string name) => new(HttpStatusCode.NotFound, "NOT_FOUND", $"{name} was not found.");
    private static ApiException BadRequest(string code, string message) => new(HttpStatusCode.BadRequest, code, message);
    private static ApiException Conflict(string code, string message) => new(HttpStatusCode.Conflict, code, message);
}
