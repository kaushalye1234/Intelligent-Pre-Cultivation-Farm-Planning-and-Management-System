namespace AgriAssist.Api.Models.Shared;

public sealed class AgentStep : AuditableEntity
{
    public Guid AgentWorkflowId { get; set; }
    public AgentWorkflow? AgentWorkflow { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string StepName { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public int CandidateRevision { get; set; } = 1;
    public string InputJson { get; set; } = "{}";
    public string OutputJson { get; set; } = "{}";
    public AgentStepStatus Status { get; set; } = AgentStepStatus.Pending;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessageSafe { get; set; }
    public List<AgentToolExecution> ToolExecutions { get; set; } = [];
}

public enum AgentStepStatus
{
    Pending = 1,
    Running = 2,
    Completed = 3,
    Failed = 4,
    Skipped = 5
}
