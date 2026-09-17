namespace AgriAssist.Api.Models.Shared;

public sealed class AgentValidationResult : AuditableEntity
{
    public Guid AgentWorkflowId { get; set; }
    public AgentWorkflow? AgentWorkflow { get; set; }
    public string ValidatorName { get; set; } = string.Empty;
    public int CandidateRevision { get; set; } = 1;
    public bool IsValid { get; set; }
    public string ErrorsJson { get; set; } = "[]";
    public string WarningsJson { get; set; } = "[]";
}
