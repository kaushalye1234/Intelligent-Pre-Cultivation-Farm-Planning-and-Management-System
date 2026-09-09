using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Dtos.TaskApproval;

namespace AgriAssist.Api.Services.TaskApproval;

public interface ITaskApprovalService
{
    Task<PagedResult<FarmTaskResponse>> SearchTasksAsync(PagedQuery query, CancellationToken cancellationToken);
    Task<FarmTaskResponse> CreateTaskAsync(FarmTaskRequest request, CancellationToken cancellationToken);
    Task<FarmTaskResponse> UpdateTaskAsync(Guid id, FarmTaskRequest request, CancellationToken cancellationToken);
    Task<PagedResult<IrrigationScheduleResponse>> SearchSchedulesAsync(PagedQuery query, CancellationToken cancellationToken);
    Task<IrrigationScheduleResponse> CreateScheduleAsync(IrrigationScheduleRequest request, CancellationToken cancellationToken);
    Task<ApprovalDecisionResponse> ApproveTaskAsync(Guid taskId, ApprovalActionRequest request, CancellationToken cancellationToken);
    Task<ApprovalDecisionResponse> RejectTaskAsync(Guid taskId, ApprovalActionRequest request, CancellationToken cancellationToken);
    Task<ApprovalDecisionResponse> RequestTaskRevisionAsync(Guid taskId, ApprovalActionRequest request, CancellationToken cancellationToken);
    Task<ApprovalDecisionResponse> ApproveScheduleAsync(Guid scheduleId, ApprovalActionRequest request, CancellationToken cancellationToken);
    Task<ApprovalDecisionResponse> RejectScheduleAsync(Guid scheduleId, ApprovalActionRequest request, CancellationToken cancellationToken);
    Task<ApprovalDecisionResponse> RequestScheduleRevisionAsync(Guid scheduleId, ApprovalActionRequest request, CancellationToken cancellationToken);
    Task<PagedResult<ApprovalDecisionResponse>> SearchApprovalsAsync(PagedQuery query, CancellationToken cancellationToken);
}
