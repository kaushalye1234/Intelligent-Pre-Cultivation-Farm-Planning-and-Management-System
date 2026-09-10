namespace AgriAssist.Api.Models.Shared;

public sealed class AgentToolExecution : AuditableEntity
{
    public Guid AgentStepId { get; set; }
    public AgentStep? AgentStep { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string InputJson { get; set; } = "{}";
    public string OutputJson { get; set; } = "{}";
    public AgentToolExecutionStatus Status { get; set; } = AgentToolExecutionStatus.Pending;
    public long DurationMs { get; set; }
}

public enum AgentToolExecutionStatus
{
    Pending = 1,
    Running = 2,
    Completed = 3,
    Failed = 4
}
