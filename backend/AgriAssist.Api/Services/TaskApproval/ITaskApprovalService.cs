using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Dtos.TaskApproval;

namespace AgriAssist.Api.Services.TaskApproval;

public interface ITaskApprovalService
{
    Task<PagedResult<FarmTaskResponse>> SearchTasksAsync(FarmTaskQuery query, CancellationToken cancellationToken);
    Task<FarmTaskResponse> GetTaskAsync(Guid id, CancellationToken cancellationToken);
    Task<FarmTaskResponse> CreateTaskAsync(FarmTaskRequest request, CancellationToken cancellationToken);
    Task<FarmTaskResponse> UpdateTaskAsync(Guid id, FarmTaskRequest request, CancellationToken cancellationToken);
    Task<FarmTaskResponse> SubmitTaskAsync(Guid id, CancellationToken cancellationToken);
    Task<FarmTaskResponse> CancelTaskAsync(Guid id, CancellationRequest request, CancellationToken cancellationToken);
    Task DeleteTaskAsync(Guid id, CancellationToken cancellationToken);
    Task<PagedResult<IrrigationScheduleResponse>> SearchSchedulesAsync(IrrigationScheduleQuery query, CancellationToken cancellationToken);
    Task<IrrigationScheduleResponse> GetScheduleAsync(Guid id, CancellationToken cancellationToken);
    Task<IrrigationScheduleResponse> CreateScheduleAsync(IrrigationScheduleRequest request, CancellationToken cancellationToken);
    Task<IrrigationScheduleResponse> UpdateScheduleAsync(Guid id, IrrigationScheduleRequest request, CancellationToken cancellationToken);
    Task<IrrigationScheduleResponse> SubmitScheduleAsync(Guid id, CancellationToken cancellationToken);
    Task<IrrigationScheduleResponse> CancelScheduleAsync(Guid id, CancellationRequest request, CancellationToken cancellationToken);
    Task DeleteScheduleAsync(Guid id, CancellationToken cancellationToken);
    Task<ApprovalDecisionResponse> ApproveTaskAsync(Guid taskId, ApprovalActionRequest request, CancellationToken cancellationToken);
    Task<ApprovalDecisionResponse> RejectTaskAsync(Guid taskId, ApprovalActionRequest request, CancellationToken cancellationToken);
    Task<ApprovalDecisionResponse> RequestTaskRevisionAsync(Guid taskId, ApprovalActionRequest request, CancellationToken cancellationToken);
    Task<ApprovalDecisionResponse> ApproveScheduleAsync(Guid scheduleId, ApprovalActionRequest request, CancellationToken cancellationToken);
    Task<ApprovalDecisionResponse> RejectScheduleAsync(Guid scheduleId, ApprovalActionRequest request, CancellationToken cancellationToken);
    Task<ApprovalDecisionResponse> RequestScheduleRevisionAsync(Guid scheduleId, ApprovalActionRequest request, CancellationToken cancellationToken);
    Task<PagedResult<ApprovalDecisionResponse>> SearchApprovalsAsync(ApprovalHistoryQuery query, CancellationToken cancellationToken);
}
