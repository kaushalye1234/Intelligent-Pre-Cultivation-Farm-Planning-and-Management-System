using AgriAssist.Api.Models.TaskApproval;
using AgriAssist.Api.Dtos.Shared;

namespace AgriAssist.Api.Dtos.TaskApproval;

public sealed record FarmTaskRequest(Guid FarmId, string Title, string Description, DateTime DueAt, Guid AssignedToUserId, FarmTaskStatus Status);
public sealed record FarmTaskResponse(Guid Id, Guid FarmId, string Title, string Description, DateTime DueAt, Guid AssignedToUserId, FarmTaskStatus Status, Guid? GeneratedByWorkflowId, int? CandidateRevision);

public sealed class FarmTaskQuery : PagedQuery
{
    public Guid? FarmId { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public FarmTaskStatus? Status { get; set; }
    public DateTime? DueFrom { get; set; }
    public DateTime? DueTo { get; set; }
}

public sealed record IrrigationScheduleRequest(Guid FieldId, DateTime ScheduledAt, int DurationMinutes, string Notes, IrrigationScheduleStatus Status);
public sealed record IrrigationScheduleResponse(Guid Id, Guid FieldId, DateTime ScheduledAt, int DurationMinutes, string Notes, IrrigationScheduleStatus Status, Guid? GeneratedByWorkflowId, int? CandidateRevision);

public sealed class IrrigationScheduleQuery : PagedQuery
{
    public Guid? FarmId { get; set; }
    public Guid? FieldId { get; set; }
    public IrrigationScheduleStatus? Status { get; set; }
    public DateTime? ScheduledFrom { get; set; }
    public DateTime? ScheduledTo { get; set; }
}

public sealed record ApprovalActionRequest(string Comment, Guid? AgentWorkflowId);
public sealed record ApprovalDecisionResponse(Guid Id, Guid? FarmTaskId, Guid? IrrigationScheduleId, Guid DecidedByUserId, Guid? AgentWorkflowId, ApprovalDecisionType Decision, string Comment, DateTime CreatedAt);

public sealed class ApprovalHistoryQuery : PagedQuery
{
    public ApprovalDecisionType? Decision { get; set; }
    public Guid? AgentWorkflowId { get; set; }
    public Guid? FarmTaskId { get; set; }
    public Guid? IrrigationScheduleId { get; set; }
}

public sealed record CancellationRequest(string Reason);
