using AgriAssist.Api.Models.CropPlanning;

namespace AgriAssist.Api.Models.Shared;

public sealed class AgentWorkflow : AuditableEntity
{
    public Guid? CropPlanRequestId { get; set; }
    public CropPlanRequest? CropPlanRequest { get; set; }
    public Guid InitiatedByUserId { get; set; }
    public AppUser? InitiatedByUser { get; set; }
    public string Objective { get; set; } = string.Empty;
    public AgentWorkflowStatus Status { get; set; } = AgentWorkflowStatus.NotStarted;
    public string CurrentStep { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
    public List<AgentStep> Steps { get; set; } = [];
    public List<AgentValidationResult> ValidationResults { get; set; } = [];
}

public enum AgentWorkflowStatus
{
    NotStarted = 1,
    Pending = 2,
    Running = 3,
    Completed = 4,
    Failed = 5,
    Cancelled = 6
}
