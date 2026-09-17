using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Dtos.TaskApproval;

namespace AgriAssist.Api.Services.TaskApproval;

public interface IWorkflowApprovalService
{
    Task<PagedResult<WorkflowSummaryResponse>> SearchAsync(WorkflowApprovalQuery query, CancellationToken cancellationToken);
    Task<WorkflowReviewResponse> GetAsync(Guid workflowId, CancellationToken cancellationToken);
    Task<WorkflowHistoryResponse> GetHistoryAsync(Guid workflowId, CancellationToken cancellationToken);
    Task<WorkflowReviewResponse> GenerateCandidateAsync(Guid workflowId, CancellationToken cancellationToken);
    Task<WorkflowDecisionResponse> ApproveAsync(Guid workflowId, WorkflowDecisionRequest request, CancellationToken cancellationToken);
    Task<WorkflowDecisionResponse> RejectAsync(Guid workflowId, WorkflowDecisionRequest request, CancellationToken cancellationToken);
    Task<WorkflowDecisionResponse> RequestRevisionAsync(Guid workflowId, WorkflowDecisionRequest request, CancellationToken cancellationToken);
}
