using System.Net;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Dtos.TaskApproval;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Models.TaskApproval;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.Shared;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Services.TaskApproval;

public sealed class TaskApprovalService(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    IRequestValidator<FarmTaskRequest> taskValidator,
    IRequestValidator<IrrigationScheduleRequest> scheduleValidator,
    IRequestValidator<ApprovalActionRequest> approvalValidator,
    IRequestValidator<CancellationRequest> cancellationValidator) : ITaskApprovalService
{
    public async Task<PagedResult<FarmTaskResponse>> SearchTasksAsync(FarmTaskQuery query, CancellationToken cancellationToken)
    {
        query.Normalize();
        ValidateTaskQuery(query);
        var tasks = ApplyTaskAccess(dbContext.FarmTasks.AsNoTracking());
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            tasks = tasks.Where(item => item.Title.ToLower().Contains(search) || item.Description.ToLower().Contains(search));
        }
        if (query.FarmId.HasValue) tasks = tasks.Where(item => item.FarmId == query.FarmId.Value);
        if (query.AssignedToUserId.HasValue) tasks = tasks.Where(item => item.AssignedToUserId == query.AssignedToUserId.Value);
        if (query.Status.HasValue) tasks = tasks.Where(item => item.Status == query.Status.Value);
        if (query.DueFrom.HasValue) tasks = tasks.Where(item => item.DueAt >= query.DueFrom.Value);
        if (query.DueTo.HasValue) tasks = tasks.Where(item => item.DueAt <= query.DueTo.Value);

        var total = await tasks.CountAsync(cancellationToken);
        var ordered = OrderTasks(tasks, query.SortBy, query.SortDirection == "desc");
        var items = await ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(item => MapTask(item)).ToListAsync(cancellationToken);
        return new PagedResult<FarmTaskResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<FarmTaskResponse> GetTaskAsync(Guid id, CancellationToken cancellationToken)
    {
        var task = await ApplyTaskAccess(dbContext.FarmTasks.AsNoTracking())
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken) ?? throw NotFound("Task");
        return MapTask(task);
    }

    public async Task<FarmTaskResponse> CreateTaskAsync(FarmTaskRequest request, CancellationToken cancellationToken)
    {
        Validate(taskValidator.Validate(request));
        RequireStaff();
        EnsureTaskInitialStatus(request.Status);
        await EnsureTaskReferencesAsync(request, cancellationToken);
        await EnsureTaskConflictFreeAsync(request.AssignedToUserId, request.DueAt, null, cancellationToken);

        var actor = RequireUser();
        var task = new FarmTask
        {
            FarmId = request.FarmId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            DueAt = request.DueAt,
            AssignedToUserId = request.AssignedToUserId,
            Status = request.Status,
            CreatedByUserId = actor,
            UpdatedByUserId = actor
        };
        dbContext.FarmTasks.Add(task);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapTask(task);
    }

    public async Task<FarmTaskResponse> UpdateTaskAsync(Guid id, FarmTaskRequest request, CancellationToken cancellationToken)
    {
        Validate(taskValidator.Validate(request));
        RequireStaff();
        var task = await ApplyTaskAccess(dbContext.FarmTasks)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken) ?? throw NotFound("Task");
        EnsureTaskEditable(task.Status);
        if (request.Status != task.Status)
            throw Conflict("TASK_STATUS_CHANGE_NOT_ALLOWED", "Use the task workflow actions to change task status.");

        await EnsureTaskReferencesAsync(request, cancellationToken);
        await EnsureTaskConflictFreeAsync(request.AssignedToUserId, request.DueAt, task.Id, cancellationToken);
        task.FarmId = request.FarmId;
        task.Title = request.Title.Trim();
        task.Description = request.Description?.Trim() ?? string.Empty;
        task.DueAt = request.DueAt;
        task.AssignedToUserId = request.AssignedToUserId;
        MarkUpdated(task);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapTask(task);
    }

    public async Task<FarmTaskResponse> SubmitTaskAsync(Guid id, CancellationToken cancellationToken)
    {
        RequireStaff();
        var task = await ApplyTaskAccess(dbContext.FarmTasks)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken) ?? throw NotFound("Task");
        if (task.Status is not (FarmTaskStatus.Draft or FarmTaskStatus.RevisionRequested))
            throw Conflict("TASK_SUBMIT_NOT_ALLOWED", "Only draft or revision-requested tasks can be submitted.");

        await EnsureTaskConflictFreeAsync(task.AssignedToUserId, task.DueAt, task.Id, cancellationToken);
        task.Status = FarmTaskStatus.PendingApproval;
        MarkUpdated(task);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapTask(task);
    }

    public async Task<FarmTaskResponse> CancelTaskAsync(Guid id, CancellationRequest request, CancellationToken cancellationToken)
    {
        Validate(cancellationValidator.Validate(request));
        RequireStaff();
        var task = await ApplyTaskAccess(dbContext.FarmTasks)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken) ?? throw NotFound("Task");
        if (task.Status is FarmTaskStatus.Cancelled or FarmTaskStatus.Completed or FarmTaskStatus.Rejected)
            throw Conflict("TASK_CANCEL_NOT_ALLOWED", "Completed, rejected, or already cancelled tasks cannot be cancelled.");

        task.Status = FarmTaskStatus.Cancelled;
        MarkUpdated(task);
        dbContext.ApprovalDecisions.Add(CreateDecision(task.Id, null, ApprovalDecisionType.Cancelled, request.Reason, null));
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapTask(task);
    }

    public async Task DeleteTaskAsync(Guid id, CancellationToken cancellationToken)
    {
        RequireStaff();
        var task = await ApplyTaskAccess(dbContext.FarmTasks)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken) ?? throw NotFound("Task");
        if (task.Status != FarmTaskStatus.Draft || await dbContext.ApprovalDecisions.AnyAsync(item => item.FarmTaskId == id, cancellationToken))
            throw Conflict("TASK_DELETE_NOT_ALLOWED", "Only draft tasks without decision history can be deleted.");
        task.IsDeleted = true;
        MarkUpdated(task);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<IrrigationScheduleResponse>> SearchSchedulesAsync(IrrigationScheduleQuery query, CancellationToken cancellationToken)
    {
        query.Normalize();
        ValidateScheduleQuery(query);
        var schedules = ApplyScheduleAccess(dbContext.IrrigationSchedules.AsNoTracking());
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            schedules = schedules.Where(item => item.Notes.ToLower().Contains(search));
        }
        if (query.FarmId.HasValue) schedules = schedules.Where(item => item.Field!.FarmId == query.FarmId.Value);
        if (query.FieldId.HasValue) schedules = schedules.Where(item => item.FieldId == query.FieldId.Value);
        if (query.Status.HasValue) schedules = schedules.Where(item => item.Status == query.Status.Value);
        if (query.ScheduledFrom.HasValue) schedules = schedules.Where(item => item.ScheduledAt >= query.ScheduledFrom.Value);
        if (query.ScheduledTo.HasValue) schedules = schedules.Where(item => item.ScheduledAt <= query.ScheduledTo.Value);

        var total = await schedules.CountAsync(cancellationToken);
        var ordered = OrderSchedules(schedules, query.SortBy, query.SortDirection == "desc");
        var items = await ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(item => MapSchedule(item)).ToListAsync(cancellationToken);
        return new PagedResult<IrrigationScheduleResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<IrrigationScheduleResponse> GetScheduleAsync(Guid id, CancellationToken cancellationToken)
    {
        var schedule = await ApplyScheduleAccess(dbContext.IrrigationSchedules.AsNoTracking())
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken) ?? throw NotFound("Irrigation schedule");
        return MapSchedule(schedule);
    }

    public async Task<IrrigationScheduleResponse> CreateScheduleAsync(IrrigationScheduleRequest request, CancellationToken cancellationToken)
    {
        Validate(scheduleValidator.Validate(request));
        RequireStaff();
        if (request.Status != IrrigationScheduleStatus.PendingApproval)
            throw Conflict("SCHEDULE_INITIAL_STATUS_INVALID", "New irrigation schedules must be pending approval.");
        await EnsureScheduleFieldAsync(request.FieldId, cancellationToken);
        await EnsureScheduleConflictFreeAsync(request.FieldId, request.ScheduledAt, request.DurationMinutes, null, cancellationToken);

        var actor = RequireUser();
        var schedule = new IrrigationSchedule
        {
            FieldId = request.FieldId,
            ScheduledAt = request.ScheduledAt,
            DurationMinutes = request.DurationMinutes,
            Notes = request.Notes?.Trim() ?? string.Empty,
            Status = IrrigationScheduleStatus.PendingApproval,
            CreatedByUserId = actor,
            UpdatedByUserId = actor
        };
        dbContext.IrrigationSchedules.Add(schedule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapSchedule(schedule);
    }

    public async Task<IrrigationScheduleResponse> UpdateScheduleAsync(Guid id, IrrigationScheduleRequest request, CancellationToken cancellationToken)
    {
        Validate(scheduleValidator.Validate(request));
        RequireStaff();
        var schedule = await ApplyScheduleAccess(dbContext.IrrigationSchedules)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken) ?? throw NotFound("Irrigation schedule");
        EnsureScheduleEditable(schedule.Status);
        if (request.Status != schedule.Status)
            throw Conflict("SCHEDULE_STATUS_CHANGE_NOT_ALLOWED", "Use the schedule workflow actions to change schedule status.");

        await EnsureScheduleFieldAsync(request.FieldId, cancellationToken);
        await EnsureScheduleConflictFreeAsync(request.FieldId, request.ScheduledAt, request.DurationMinutes, schedule.Id, cancellationToken);
        schedule.FieldId = request.FieldId;
        schedule.ScheduledAt = request.ScheduledAt;
        schedule.DurationMinutes = request.DurationMinutes;
        schedule.Notes = request.Notes?.Trim() ?? string.Empty;
        MarkUpdated(schedule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapSchedule(schedule);
    }

    public async Task<IrrigationScheduleResponse> SubmitScheduleAsync(Guid id, CancellationToken cancellationToken)
    {
        RequireStaff();
        var schedule = await ApplyScheduleAccess(dbContext.IrrigationSchedules)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken) ?? throw NotFound("Irrigation schedule");
        if (schedule.Status != IrrigationScheduleStatus.RevisionRequested)
            throw Conflict("SCHEDULE_SUBMIT_NOT_ALLOWED", "Only revision-requested schedules can be resubmitted.");

        await EnsureScheduleConflictFreeAsync(schedule.FieldId, schedule.ScheduledAt, schedule.DurationMinutes, schedule.Id, cancellationToken);
        schedule.Status = IrrigationScheduleStatus.PendingApproval;
        MarkUpdated(schedule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapSchedule(schedule);
    }

    public async Task<IrrigationScheduleResponse> CancelScheduleAsync(Guid id, CancellationRequest request, CancellationToken cancellationToken)
    {
        Validate(cancellationValidator.Validate(request));
        RequireStaff();
        var schedule = await ApplyScheduleAccess(dbContext.IrrigationSchedules)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken) ?? throw NotFound("Irrigation schedule");
        if (schedule.Status is IrrigationScheduleStatus.Cancelled or IrrigationScheduleStatus.Completed or IrrigationScheduleStatus.Rejected)
            throw Conflict("SCHEDULE_CANCEL_NOT_ALLOWED", "Completed, rejected, or already cancelled schedules cannot be cancelled.");

        schedule.Status = IrrigationScheduleStatus.Cancelled;
        MarkUpdated(schedule);
        dbContext.ApprovalDecisions.Add(CreateDecision(null, schedule.Id, ApprovalDecisionType.Cancelled, request.Reason, null));
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapSchedule(schedule);
    }

    public async Task DeleteScheduleAsync(Guid id, CancellationToken cancellationToken)
    {
        RequireStaff();
        var schedule = await ApplyScheduleAccess(dbContext.IrrigationSchedules)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken) ?? throw NotFound("Irrigation schedule");
        if (schedule.Status != IrrigationScheduleStatus.PendingApproval || await dbContext.ApprovalDecisions.AnyAsync(item => item.IrrigationScheduleId == id, cancellationToken))
            throw Conflict("SCHEDULE_DELETE_NOT_ALLOWED", "Only pending schedules without decision history can be deleted.");
        schedule.IsDeleted = true;
        MarkUpdated(schedule);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<ApprovalDecisionResponse> ApproveTaskAsync(Guid taskId, ApprovalActionRequest request, CancellationToken cancellationToken) =>
        DecideTaskAsync(taskId, request, ApprovalDecisionType.Approved, FarmTaskStatus.Approved, cancellationToken);

    public Task<ApprovalDecisionResponse> RejectTaskAsync(Guid taskId, ApprovalActionRequest request, CancellationToken cancellationToken) =>
        DecideTaskAsync(taskId, request, ApprovalDecisionType.Rejected, FarmTaskStatus.Rejected, cancellationToken);

    public Task<ApprovalDecisionResponse> RequestTaskRevisionAsync(Guid taskId, ApprovalActionRequest request, CancellationToken cancellationToken) =>
        DecideTaskAsync(taskId, request, ApprovalDecisionType.RevisionRequested, FarmTaskStatus.RevisionRequested, cancellationToken);

    public Task<ApprovalDecisionResponse> ApproveScheduleAsync(Guid scheduleId, ApprovalActionRequest request, CancellationToken cancellationToken) =>
        DecideScheduleAsync(scheduleId, request, ApprovalDecisionType.Approved, IrrigationScheduleStatus.Approved, cancellationToken);

    public Task<ApprovalDecisionResponse> RejectScheduleAsync(Guid scheduleId, ApprovalActionRequest request, CancellationToken cancellationToken) =>
        DecideScheduleAsync(scheduleId, request, ApprovalDecisionType.Rejected, IrrigationScheduleStatus.Rejected, cancellationToken);

    public Task<ApprovalDecisionResponse> RequestScheduleRevisionAsync(Guid scheduleId, ApprovalActionRequest request, CancellationToken cancellationToken) =>
        DecideScheduleAsync(scheduleId, request, ApprovalDecisionType.RevisionRequested, IrrigationScheduleStatus.RevisionRequested, cancellationToken);

    public async Task<PagedResult<ApprovalDecisionResponse>> SearchApprovalsAsync(ApprovalHistoryQuery query, CancellationToken cancellationToken)
    {
        query.Normalize();
        if (query.Decision.HasValue && !Enum.IsDefined(query.Decision.Value))
            throw BadRequest("APPROVAL_FILTER_INVALID", "Approval decision filter is invalid.");
        var approvals = ApplyApprovalAccess(dbContext.ApprovalDecisions.AsNoTracking().Where(item => !item.IsDeleted));
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            approvals = approvals.Where(item => item.Comment.ToLower().Contains(search));
        }
        if (query.Decision.HasValue) approvals = approvals.Where(item => item.Decision == query.Decision.Value);
        if (query.AgentWorkflowId.HasValue) approvals = approvals.Where(item => item.AgentWorkflowId == query.AgentWorkflowId.Value);
        if (query.FarmTaskId.HasValue) approvals = approvals.Where(item => item.FarmTaskId == query.FarmTaskId.Value);
        if (query.IrrigationScheduleId.HasValue) approvals = approvals.Where(item => item.IrrigationScheduleId == query.IrrigationScheduleId.Value);

        var total = await approvals.CountAsync(cancellationToken);
        var ordered = OrderApprovals(approvals, query.SortBy, query.SortDirection == "desc");
        var items = await ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(item => MapApproval(item)).ToListAsync(cancellationToken);
        return new PagedResult<ApprovalDecisionResponse>(items, query.Page, query.PageSize, total);
    }

    private async Task<ApprovalDecisionResponse> DecideTaskAsync(Guid taskId, ApprovalActionRequest request, ApprovalDecisionType decision, FarmTaskStatus nextStatus, CancellationToken cancellationToken)
    {
        Validate(approvalValidator.Validate(request));
        RequireDecisionComment(decision, request.Comment);
        RequireApprover();
        await EnsureWorkflowExistsAsync(request.AgentWorkflowId, cancellationToken);
        var task = await dbContext.FarmTasks.SingleOrDefaultAsync(item => item.Id == taskId && !item.IsDeleted, cancellationToken) ?? throw NotFound("Task");
        if (task.Status != FarmTaskStatus.PendingApproval)
            throw Conflict("TASK_DECISION_NOT_ALLOWED", "Only tasks pending approval can receive a decision.");

        task.Status = nextStatus;
        MarkUpdated(task);
        var approval = CreateDecision(task.Id, null, decision, request.Comment, request.AgentWorkflowId);
        dbContext.ApprovalDecisions.Add(approval);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapApproval(approval);
    }

    private async Task<ApprovalDecisionResponse> DecideScheduleAsync(Guid scheduleId, ApprovalActionRequest request, ApprovalDecisionType decision, IrrigationScheduleStatus nextStatus, CancellationToken cancellationToken)
    {
        Validate(approvalValidator.Validate(request));
        RequireDecisionComment(decision, request.Comment);
        RequireApprover();
        await EnsureWorkflowExistsAsync(request.AgentWorkflowId, cancellationToken);
        var schedule = await dbContext.IrrigationSchedules.SingleOrDefaultAsync(item => item.Id == scheduleId && !item.IsDeleted, cancellationToken) ?? throw NotFound("Irrigation schedule");
        if (schedule.Status != IrrigationScheduleStatus.PendingApproval)
            throw Conflict("SCHEDULE_DECISION_NOT_ALLOWED", "Only schedules pending approval can receive a decision.");

        schedule.Status = nextStatus;
        MarkUpdated(schedule);
        var approval = CreateDecision(null, schedule.Id, decision, request.Comment, request.AgentWorkflowId);
        dbContext.ApprovalDecisions.Add(approval);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapApproval(approval);
    }

    private IQueryable<FarmTask> ApplyTaskAccess(IQueryable<FarmTask> query)
    {
        query = query.Include(item => item.Farm).Where(item => !item.IsDeleted);
        return currentUser.Role == ApplicationRole.Farmer
            ? query.Where(item => item.Farm!.OwnerUserId == currentUser.UserId || item.AssignedToUserId == currentUser.UserId)
            : query;
    }

    private IQueryable<IrrigationSchedule> ApplyScheduleAccess(IQueryable<IrrigationSchedule> query)
    {
        query = query.Include(item => item.Field)!.ThenInclude(field => field!.Farm).Where(item => !item.IsDeleted);
        return currentUser.Role == ApplicationRole.Farmer
            ? query.Where(item => item.Field!.Farm!.OwnerUserId == currentUser.UserId)
            : query;
    }

    private IQueryable<ApprovalDecision> ApplyApprovalAccess(IQueryable<ApprovalDecision> query)
    {
        if (currentUser.Role != ApplicationRole.Farmer) return query;
        var userId = RequireUser();
        return query.Where(item =>
            (item.FarmTask != null && (item.FarmTask.Farm!.OwnerUserId == userId || item.FarmTask.AssignedToUserId == userId)) ||
            (item.IrrigationSchedule != null && item.IrrigationSchedule.Field!.Farm!.OwnerUserId == userId));
    }

    private async Task EnsureTaskReferencesAsync(FarmTaskRequest request, CancellationToken cancellationToken)
    {
        if (!await dbContext.Farms.AnyAsync(item => item.Id == request.FarmId && !item.IsDeleted, cancellationToken)) throw NotFound("Farm");
        if (!await dbContext.Users.AnyAsync(item => item.Id == request.AssignedToUserId && item.IsActive && !item.IsDeleted, cancellationToken)) throw NotFound("Assigned user");
    }

    private async Task EnsureScheduleFieldAsync(Guid fieldId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Fields.AnyAsync(item => item.Id == fieldId && item.IsActive && !item.IsDeleted && item.Farm != null && !item.Farm.IsDeleted, cancellationToken))
            throw NotFound("Field");
    }

    private async Task EnsureTaskConflictFreeAsync(Guid assignedToUserId, DateTime dueAt, Guid? excludedId, CancellationToken cancellationToken)
    {
        var hasConflict = await dbContext.FarmTasks.AnyAsync(item =>
            !item.IsDeleted && item.Id != excludedId && item.AssignedToUserId == assignedToUserId && item.DueAt == dueAt &&
            item.Status != FarmTaskStatus.Rejected && item.Status != FarmTaskStatus.Completed && item.Status != FarmTaskStatus.Cancelled,
            cancellationToken);
        if (hasConflict) throw Conflict("TASK_SCHEDULE_CONFLICT", "The assigned user already has a task due at this time.");
    }

    private async Task EnsureScheduleConflictFreeAsync(Guid fieldId, DateTime scheduledAt, int durationMinutes, Guid? excludedId, CancellationToken cancellationToken)
    {
        var scheduledEnd = scheduledAt.AddMinutes(durationMinutes);
        var hasConflict = await dbContext.IrrigationSchedules.AnyAsync(item =>
            !item.IsDeleted && item.Id != excludedId && item.FieldId == fieldId &&
            item.Status != IrrigationScheduleStatus.Rejected && item.Status != IrrigationScheduleStatus.Completed && item.Status != IrrigationScheduleStatus.Cancelled &&
            item.ScheduledAt < scheduledEnd && item.ScheduledAt.AddMinutes(item.DurationMinutes) > scheduledAt,
            cancellationToken);
        if (hasConflict) throw Conflict("IRRIGATION_SCHEDULE_CONFLICT", "The irrigation schedule overlaps an existing schedule for this field.");
    }

    private async Task EnsureWorkflowExistsAsync(Guid? workflowId, CancellationToken cancellationToken)
    {
        if (workflowId.HasValue && !await dbContext.AgentWorkflows.AnyAsync(item => item.Id == workflowId.Value && !item.IsDeleted, cancellationToken))
            throw NotFound("Agent workflow");
    }

    private ApprovalDecision CreateDecision(Guid? taskId, Guid? scheduleId, ApprovalDecisionType decision, string? comment, Guid? workflowId)
    {
        var actor = RequireUser();
        return new ApprovalDecision
        {
            FarmTaskId = taskId,
            IrrigationScheduleId = scheduleId,
            DecidedByUserId = actor,
            AgentWorkflowId = workflowId,
            Decision = decision,
            Comment = comment?.Trim() ?? string.Empty,
            CreatedByUserId = actor,
            UpdatedByUserId = actor
        };
    }

    private void MarkUpdated(AuditableEntity entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = RequireUser();
    }

    private static IOrderedQueryable<FarmTask> OrderTasks(IQueryable<FarmTask> query, string? sortBy, bool descending)
    {
        var key = sortBy?.Trim().ToLowerInvariant();
        IOrderedQueryable<FarmTask> ordered = (key, descending) switch
        {
            ("title", false) => query.OrderBy(item => item.Title),
            ("title", true) => query.OrderByDescending(item => item.Title),
            ("status", false) => query.OrderBy(item => item.Status),
            ("status", true) => query.OrderByDescending(item => item.Status),
            ("createdat", false) => query.OrderBy(item => item.CreatedAt),
            ("createdat", true) => query.OrderByDescending(item => item.CreatedAt),
            (_, true) => query.OrderByDescending(item => item.DueAt),
            _ => query.OrderBy(item => item.DueAt)
        };
        return descending ? ordered.ThenByDescending(item => item.Id) : ordered.ThenBy(item => item.Id);
    }

    private static IOrderedQueryable<IrrigationSchedule> OrderSchedules(IQueryable<IrrigationSchedule> query, string? sortBy, bool descending)
    {
        var key = sortBy?.Trim().ToLowerInvariant();
        IOrderedQueryable<IrrigationSchedule> ordered = (key, descending) switch
        {
            ("durationminutes", false) => query.OrderBy(item => item.DurationMinutes),
            ("durationminutes", true) => query.OrderByDescending(item => item.DurationMinutes),
            ("status", false) => query.OrderBy(item => item.Status),
            ("status", true) => query.OrderByDescending(item => item.Status),
            ("createdat", false) => query.OrderBy(item => item.CreatedAt),
            ("createdat", true) => query.OrderByDescending(item => item.CreatedAt),
            (_, true) => query.OrderByDescending(item => item.ScheduledAt),
            _ => query.OrderBy(item => item.ScheduledAt)
        };
        return descending ? ordered.ThenByDescending(item => item.Id) : ordered.ThenBy(item => item.Id);
    }

    private static IOrderedQueryable<ApprovalDecision> OrderApprovals(IQueryable<ApprovalDecision> query, string? sortBy, bool descending)
    {
        var key = sortBy?.Trim().ToLowerInvariant();
        IOrderedQueryable<ApprovalDecision> ordered = (key, descending) switch
        {
            ("decision", false) => query.OrderBy(item => item.Decision),
            ("decision", true) => query.OrderByDescending(item => item.Decision),
            (_, false) => query.OrderBy(item => item.CreatedAt),
            _ => query.OrderByDescending(item => item.CreatedAt)
        };
        return descending ? ordered.ThenByDescending(item => item.Id) : ordered.ThenBy(item => item.Id);
    }

    private static void EnsureTaskInitialStatus(FarmTaskStatus status)
    {
        if (status is not (FarmTaskStatus.Draft or FarmTaskStatus.PendingApproval))
            throw Conflict("TASK_INITIAL_STATUS_INVALID", "New tasks must be draft or pending approval.");
    }

    private static void ValidateTaskQuery(FarmTaskQuery query)
    {
        if (query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
            throw BadRequest("TASK_FILTER_INVALID", "Task status filter is invalid.");
        if (query.DueFrom.HasValue && query.DueTo.HasValue && query.DueFrom > query.DueTo)
            throw BadRequest("TASK_DATE_RANGE_INVALID", "Task due-from date must not be after due-to date.");
    }

    private static void ValidateScheduleQuery(IrrigationScheduleQuery query)
    {
        if (query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
            throw BadRequest("SCHEDULE_FILTER_INVALID", "Schedule status filter is invalid.");
        if (query.ScheduledFrom.HasValue && query.ScheduledTo.HasValue && query.ScheduledFrom > query.ScheduledTo)
            throw BadRequest("SCHEDULE_DATE_RANGE_INVALID", "Schedule start date must not be after end date.");
    }

    private static void EnsureTaskEditable(FarmTaskStatus status)
    {
        if (status is not (FarmTaskStatus.Draft or FarmTaskStatus.PendingApproval or FarmTaskStatus.RevisionRequested))
            throw Conflict("TASK_EDIT_NOT_ALLOWED", "Only draft, pending, or revision-requested tasks can be edited.");
    }

    private static void EnsureScheduleEditable(IrrigationScheduleStatus status)
    {
        if (status is not (IrrigationScheduleStatus.PendingApproval or IrrigationScheduleStatus.RevisionRequested))
            throw Conflict("SCHEDULE_EDIT_NOT_ALLOWED", "Only pending or revision-requested schedules can be edited.");
    }

    private static void RequireDecisionComment(ApprovalDecisionType decision, string? comment)
    {
        if (decision is ApprovalDecisionType.Rejected or ApprovalDecisionType.RevisionRequested && string.IsNullOrWhiteSpace(comment))
            throw new ApiException(HttpStatusCode.BadRequest, "DECISION_COMMENT_REQUIRED", "A comment is required when rejecting or requesting revision.");
    }

    private void RequireStaff()
    {
        if (currentUser.Role is ApplicationRole.Farmer or null)
            throw new ApiException(HttpStatusCode.Forbidden, "STAFF_REQUIRED", "A staff role is required.");
    }

    private void RequireApprover()
    {
        if (currentUser.Role is not (ApplicationRole.AgriculturalOfficer or ApplicationRole.Admin))
            throw new ApiException(HttpStatusCode.Forbidden, "APPROVER_REQUIRED", "AgriculturalOfficer or Admin role is required.");
    }

    private Guid RequireUser() => currentUser.UserId ?? throw new ApiException(HttpStatusCode.Unauthorized, "AUTH_REQUIRED", "Authentication is required.");
    private static ApiException NotFound(string name) => new(HttpStatusCode.NotFound, "NOT_FOUND", $"{name} was not found.");
    private static ApiException BadRequest(string code, string message) => new(HttpStatusCode.BadRequest, code, message);
    private static ApiException Conflict(string code, string message) => new(HttpStatusCode.Conflict, code, message);
    private static void Validate(IReadOnlyList<string> errors)
    {
        if (errors.Count > 0) throw new ApiException(HttpStatusCode.BadRequest, "VALIDATION_ERROR", string.Join(" ", errors));
    }

    private static FarmTaskResponse MapTask(FarmTask item) => new(item.Id, item.FarmId, item.Title, item.Description, item.DueAt, item.AssignedToUserId, item.Status);
    private static IrrigationScheduleResponse MapSchedule(IrrigationSchedule item) => new(item.Id, item.FieldId, item.ScheduledAt, item.DurationMinutes, item.Notes, item.Status);
    private static ApprovalDecisionResponse MapApproval(ApprovalDecision item) => new(item.Id, item.FarmTaskId, item.IrrigationScheduleId, item.DecidedByUserId, item.AgentWorkflowId, item.Decision, item.Comment, item.CreatedAt);
}
