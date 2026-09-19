using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Models.TaskApproval;

public sealed class FarmTask : AuditableEntity
{
    public Guid FarmId { get; set; }
    public Farm? Farm { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime DueAt { get; set; }
    public FarmTaskStatus Status { get; set; } = FarmTaskStatus.PendingApproval;
    public Guid AssignedToUserId { get; set; }
    public AppUser? AssignedToUser { get; set; }
    public Guid? GeneratedByWorkflowId { get; set; }
    public AgentWorkflow? GeneratedByWorkflow { get; set; }
    public int? CandidateRevision { get; set; }
}

public enum FarmTaskStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Rejected = 4,
    RevisionRequested = 5,
    Completed = 6,
    Cancelled = 7
}
