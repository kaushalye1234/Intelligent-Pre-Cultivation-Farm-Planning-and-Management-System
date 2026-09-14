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
    public async Task<ActionResult<PagedResult<FarmTaskResponse>>> SearchTasks([FromQuery] FarmTaskQuery query, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.SearchTasksAsync(query, cancellationToken));

    [HttpGet("tasks/{id:guid}")]
    public async Task<ActionResult<FarmTaskResponse>> GetTask(Guid id, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.GetTaskAsync(id, cancellationToken));

    [HttpPost("tasks")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<FarmTaskResponse>> CreateTask(FarmTaskRequest request, CancellationToken cancellationToken)
    {
        var created = await taskApprovalService.CreateTaskAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetTask), new { id = created.Id }, created);
    }

    [HttpPut("tasks/{id:guid}")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<FarmTaskResponse>> UpdateTask(Guid id, FarmTaskRequest request, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.UpdateTaskAsync(id, request, cancellationToken));

    [HttpPost("tasks/{id:guid}/submit")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<FarmTaskResponse>> SubmitTask(Guid id, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.SubmitTaskAsync(id, cancellationToken));

    [HttpPost("tasks/{id:guid}/cancel")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<FarmTaskResponse>> CancelTask(Guid id, CancellationRequest request, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.CancelTaskAsync(id, request, cancellationToken));

    [HttpDelete("tasks/{id:guid}")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<IActionResult> DeleteTask(Guid id, CancellationToken cancellationToken)
    {
        await taskApprovalService.DeleteTaskAsync(id, cancellationToken);
        return NoContent();
    }

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
    public async Task<ActionResult<PagedResult<IrrigationScheduleResponse>>> SearchSchedules([FromQuery] IrrigationScheduleQuery query, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.SearchSchedulesAsync(query, cancellationToken));

    [HttpGet("schedules/{id:guid}")]
    public async Task<ActionResult<IrrigationScheduleResponse>> GetSchedule(Guid id, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.GetScheduleAsync(id, cancellationToken));

    [HttpPost("schedules")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<IrrigationScheduleResponse>> CreateSchedule(IrrigationScheduleRequest request, CancellationToken cancellationToken)
    {
        var created = await taskApprovalService.CreateScheduleAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetSchedule), new { id = created.Id }, created);
    }

    [HttpPut("schedules/{id:guid}")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<IrrigationScheduleResponse>> UpdateSchedule(Guid id, IrrigationScheduleRequest request, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.UpdateScheduleAsync(id, request, cancellationToken));

    [HttpPost("schedules/{id:guid}/submit")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<IrrigationScheduleResponse>> SubmitSchedule(Guid id, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.SubmitScheduleAsync(id, cancellationToken));

    [HttpPost("schedules/{id:guid}/cancel")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<IrrigationScheduleResponse>> CancelSchedule(Guid id, CancellationRequest request, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.CancelScheduleAsync(id, request, cancellationToken));

    [HttpDelete("schedules/{id:guid}")]
    [Authorize(Roles = $"{nameof(ApplicationRole.FieldOfficer)},{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<IActionResult> DeleteSchedule(Guid id, CancellationToken cancellationToken)
    {
        await taskApprovalService.DeleteScheduleAsync(id, cancellationToken);
        return NoContent();
    }

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
    public async Task<ActionResult<PagedResult<ApprovalDecisionResponse>>> SearchApprovals([FromQuery] ApprovalHistoryQuery query, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.SearchApprovalsAsync(query, cancellationToken));
}
