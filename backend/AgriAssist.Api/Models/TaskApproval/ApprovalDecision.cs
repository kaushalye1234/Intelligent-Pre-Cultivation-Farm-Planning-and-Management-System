using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Models.TaskApproval;

public sealed class ApprovalDecision : AuditableEntity
{
    public Guid? FarmTaskId { get; set; }
    public FarmTask? FarmTask { get; set; }
    public Guid? IrrigationScheduleId { get; set; }
    public IrrigationSchedule? IrrigationSchedule { get; set; }
    public Guid DecidedByUserId { get; set; }
    public AppUser? DecidedByUser { get; set; }
    public Guid? AgentWorkflowId { get; set; }
    public AgentWorkflow? AgentWorkflow { get; set; }
    public ApprovalDecisionType Decision { get; set; }
    public string Comment { get; set; } = string.Empty;
}

public enum ApprovalDecisionType
{
    Approved = 1,
    Rejected = 2,
    RevisionRequested = 3,
    Cancelled = 4
}
