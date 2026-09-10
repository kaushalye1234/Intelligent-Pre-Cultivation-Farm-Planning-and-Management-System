using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Dtos.TaskApproval;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.TaskApproval;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriAssist.Api.Controllers.TaskApproval;

[ApiController]
[Route("api/task-approval")]
[Authorize]
public sealed class TaskApprovalController(ITaskApprovalService taskApprovalService) : ControllerBase
{
    [HttpGet("tasks")]
    public async Task<ActionResult<PagedResult<FarmTaskResponse>>> SearchTasks([FromQuery] PagedQuery query, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.SearchTasksAsync(query, cancellationToken));

    [HttpPost("tasks")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<FarmTaskResponse>> CreateTask(FarmTaskRequest request, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.CreateTaskAsync(request, cancellationToken));

    [HttpPut("tasks/{id:guid}")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<FarmTaskResponse>> UpdateTask(Guid id, FarmTaskRequest request, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.UpdateTaskAsync(id, request, cancellationToken));

    [HttpPost("tasks/{id:guid}/approve")]
    [Authorize(Roles = $"{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<ApprovalDecisionResponse>> ApproveTask(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.ApproveTaskAsync(id, request, cancellationToken));

    [HttpPost("tasks/{id:guid}/reject")]
    [Authorize(Roles = $"{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<ApprovalDecisionResponse>> RejectTask(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.RejectTaskAsync(id, request, cancellationToken));

    [HttpPost("tasks/{id:guid}/request-revision")]
    [Authorize(Roles = $"{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<ApprovalDecisionResponse>> RequestTaskRevision(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.RequestTaskRevisionAsync(id, request, cancellationToken));

    [HttpGet("schedules")]
    public async Task<ActionResult<PagedResult<IrrigationScheduleResponse>>> SearchSchedules([FromQuery] PagedQuery query, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.SearchSchedulesAsync(query, cancellationToken));

    [HttpPost("schedules")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<IrrigationScheduleResponse>> CreateSchedule(IrrigationScheduleRequest request, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.CreateScheduleAsync(request, cancellationToken));

    [HttpPost("schedules/{id:guid}/approve")]
    [Authorize(Roles = $"{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<ApprovalDecisionResponse>> ApproveSchedule(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.ApproveScheduleAsync(id, request, cancellationToken));

    [HttpPost("schedules/{id:guid}/reject")]
    [Authorize(Roles = $"{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<ApprovalDecisionResponse>> RejectSchedule(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.RejectScheduleAsync(id, request, cancellationToken));

    [HttpPost("schedules/{id:guid}/request-revision")]
    [Authorize(Roles = $"{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<ApprovalDecisionResponse>> RequestScheduleRevision(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.RequestScheduleRevisionAsync(id, request, cancellationToken));

    [HttpGet("approvals")]
    public async Task<ActionResult<PagedResult<ApprovalDecisionResponse>>> SearchApprovals([FromQuery] PagedQuery query, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.SearchApprovalsAsync(query, cancellationToken));
}
