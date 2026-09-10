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
    IRequestValidator<ApprovalActionRequest> approvalValidator) : ITaskApprovalService
{
    public async Task<PagedResult<FarmTaskResponse>> SearchTasksAsync(PagedQuery query, CancellationToken cancellationToken)
    {
        query.Normalize();
        var tasks = ApplyTaskAccess(dbContext.FarmTasks.AsNoTracking());
        if (!string.IsNullOrWhiteSpace(query.Search)) tasks = tasks.Where(item => item.Title.ToLower().Contains(query.Search.ToLower()));
        tasks = query.SortDirection == "desc" ? tasks.OrderByDescending(item => item.DueAt) : tasks.OrderBy(item => item.DueAt);
        var total = await tasks.CountAsync(cancellationToken);
        var items = await tasks.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(item => MapTask(item)).ToListAsync(cancellationToken);
        return new PagedResult<FarmTaskResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<FarmTaskResponse> CreateTaskAsync(FarmTaskRequest request, CancellationToken cancellationToken)
    {
        Validate(taskValidator.Validate(request));
        RequireStaff();
        if (!await dbContext.Farms.AnyAsync(item => item.Id == request.FarmId && !item.IsDeleted, cancellationToken)) throw NotFound("Farm");
        if (!await dbContext.Users.AnyAsync(item => item.Id == request.AssignedToUserId && item.IsActive, cancellationToken)) throw NotFound("Assigned user");
        var task = new FarmTask { FarmId = request.FarmId, Title = request.Title.Trim(), Description = request.Description.Trim(), DueAt = request.DueAt, AssignedToUserId = request.AssignedToUserId, Status = request.Status, CreatedByUserId = currentUser.UserId };
        dbContext.FarmTasks.Add(task);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapTask(task);
    }

    public async Task<FarmTaskResponse> UpdateTaskAsync(Guid id, FarmTaskRequest request, CancellationToken cancellationToken)
    {
        Validate(taskValidator.Validate(request));
        var task = await ApplyTaskAccess(dbContext.FarmTasks).SingleOrDefaultAsync(item => item.Id == id, cancellationToken) ?? throw NotFound("Task");
        task.Title = request.Title.Trim();
        task.Description = request.Description.Trim();
        task.DueAt = request.DueAt;
        task.Status = request.Status;
        task.AssignedToUserId = request.AssignedToUserId;
        task.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapTask(task);
    }

    public async Task<PagedResult<IrrigationScheduleResponse>> SearchSchedulesAsync(PagedQuery query, CancellationToken cancellationToken)
    {
        query.Normalize();
        var schedules = ApplyScheduleAccess(dbContext.IrrigationSchedules.AsNoTracking());
        schedules = query.SortDirection == "desc" ? schedules.OrderByDescending(item => item.ScheduledAt) : schedules.OrderBy(item => item.ScheduledAt);
        var total = await schedules.CountAsync(cancellationToken);
        var items = await schedules.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(item => MapSchedule(item)).ToListAsync(cancellationToken);
        return new PagedResult<IrrigationScheduleResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<IrrigationScheduleResponse> CreateScheduleAsync(IrrigationScheduleRequest request, CancellationToken cancellationToken)
    {
        Validate(scheduleValidator.Validate(request));
        RequireStaff();
        if (!await dbContext.Fields.AnyAsync(item => item.Id == request.FieldId && !item.IsDeleted, cancellationToken)) throw NotFound("Field");
        var schedule = new IrrigationSchedule { FieldId = request.FieldId, ScheduledAt = request.ScheduledAt, DurationMinutes = request.DurationMinutes, Notes = request.Notes.Trim(), Status = request.Status, CreatedByUserId = currentUser.UserId };
        dbContext.IrrigationSchedules.Add(schedule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapSchedule(schedule);
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

    public async Task<PagedResult<ApprovalDecisionResponse>> SearchApprovalsAsync(PagedQuery query, CancellationToken cancellationToken)
    {
        query.Normalize();
        var approvals = dbContext.ApprovalDecisions.AsNoTracking().Where(item => !item.IsDeleted).OrderByDescending(item => item.CreatedAt);
        var total = await approvals.CountAsync(cancellationToken);
        var items = await approvals.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(item => MapApproval(item)).ToListAsync(cancellationToken);
        return new PagedResult<ApprovalDecisionResponse>(items, query.Page, query.PageSize, total);
    }

    private async Task<ApprovalDecisionResponse> DecideTaskAsync(Guid taskId, ApprovalActionRequest request, ApprovalDecisionType decision, FarmTaskStatus nextStatus, CancellationToken cancellationToken)
    {
        Validate(approvalValidator.Validate(request));
        RequireApprover();
        await EnsureWorkflowExistsAsync(request.AgentWorkflowId, cancellationToken);
        var task = await dbContext.FarmTasks.SingleOrDefaultAsync(item => item.Id == taskId && !item.IsDeleted, cancellationToken) ?? throw NotFound("Task");
        if (task.Status != FarmTaskStatus.PendingApproval) throw new ApiException(HttpStatusCode.Conflict, "TASK_DECISION_NOT_ALLOWED", "Only tasks pending approval can receive a decision.");
        task.Status = nextStatus;
        task.UpdatedAt = DateTime.UtcNow;
        var approval = new ApprovalDecision { FarmTaskId = task.Id, DecidedByUserId = RequireUser(), AgentWorkflowId = request.AgentWorkflowId, Decision = decision, Comment = request.Comment.Trim(), CreatedByUserId = currentUser.UserId };
        dbContext.ApprovalDecisions.Add(approval);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapApproval(approval);
    }

    private async Task<ApprovalDecisionResponse> DecideScheduleAsync(Guid scheduleId, ApprovalActionRequest request, ApprovalDecisionType decision, IrrigationScheduleStatus nextStatus, CancellationToken cancellationToken)
    {
        Validate(approvalValidator.Validate(request));
        RequireApprover();
        await EnsureWorkflowExistsAsync(request.AgentWorkflowId, cancellationToken);
        var schedule = await dbContext.IrrigationSchedules.SingleOrDefaultAsync(item => item.Id == scheduleId && !item.IsDeleted, cancellationToken) ?? throw NotFound("Irrigation schedule");
        if (schedule.Status != IrrigationScheduleStatus.PendingApproval) throw new ApiException(HttpStatusCode.Conflict, "SCHEDULE_DECISION_NOT_ALLOWED", "Only schedules pending approval can receive a decision.");
        schedule.Status = nextStatus;
        schedule.UpdatedAt = DateTime.UtcNow;
        var approval = new ApprovalDecision { IrrigationScheduleId = schedule.Id, DecidedByUserId = RequireUser(), AgentWorkflowId = request.AgentWorkflowId, Decision = decision, Comment = request.Comment.Trim(), CreatedByUserId = currentUser.UserId };
        dbContext.ApprovalDecisions.Add(approval);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapApproval(approval);
    }

    private IQueryable<FarmTask> ApplyTaskAccess(IQueryable<FarmTask> query)
    {
        query = query.Include(item => item.Farm).Where(item => !item.IsDeleted);
        return currentUser.Role == ApplicationRole.Farmer ? query.Where(item => item.Farm!.OwnerUserId == currentUser.UserId || item.AssignedToUserId == currentUser.UserId) : query;
    }

    private IQueryable<IrrigationSchedule> ApplyScheduleAccess(IQueryable<IrrigationSchedule> query)
    {
        query = query.Include(item => item.Field)!.ThenInclude(field => field!.Farm).Where(item => !item.IsDeleted);
        return currentUser.Role == ApplicationRole.Farmer ? query.Where(item => item.Field!.Farm!.OwnerUserId == currentUser.UserId) : query;
    }

    private async Task EnsureWorkflowExistsAsync(Guid? workflowId, CancellationToken cancellationToken)
    {
        if (workflowId.HasValue && !await dbContext.AgentWorkflows.AnyAsync(item => item.Id == workflowId.Value, cancellationToken)) throw NotFound("Agent workflow");
    }

    private void RequireStaff()
    {
        if (currentUser.Role is ApplicationRole.Farmer or null) throw new ApiException(HttpStatusCode.Forbidden, "STAFF_REQUIRED", "A staff role is required.");
    }

    private void RequireApprover()
    {
        if (currentUser.Role is not (ApplicationRole.AgriculturalOfficer or ApplicationRole.Admin)) throw new ApiException(HttpStatusCode.Forbidden, "APPROVER_REQUIRED", "AgriculturalOfficer or Admin role is required.");
    }

    private Guid RequireUser() => currentUser.UserId ?? throw new ApiException(HttpStatusCode.Unauthorized, "AUTH_REQUIRED", "Authentication is required.");
    private static ApiException NotFound(string name) => new(HttpStatusCode.NotFound, "NOT_FOUND", $"{name} was not found.");
    private static void Validate(IReadOnlyList<string> errors) { if (errors.Count > 0) throw new ApiException(HttpStatusCode.BadRequest, "VALIDATION_ERROR", string.Join(" ", errors)); }
    private static FarmTaskResponse MapTask(FarmTask item) => new(item.Id, item.FarmId, item.Title, item.Description, item.DueAt, item.AssignedToUserId, item.Status);
    private static IrrigationScheduleResponse MapSchedule(IrrigationSchedule item) => new(item.Id, item.FieldId, item.ScheduledAt, item.DurationMinutes, item.Notes, item.Status);
    private static ApprovalDecisionResponse MapApproval(ApprovalDecision item) => new(item.Id, item.FarmTaskId, item.IrrigationScheduleId, item.DecidedByUserId, item.AgentWorkflowId, item.Decision, item.Comment, item.CreatedAt);
}
