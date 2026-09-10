namespace AgriAssist.Api.ExternalServices.AgenticAI;

public sealed class AgenticAIClient(ILogger<AgenticAIClient> logger) : IAgenticAIClient
{
    public Task StartWorkflowAsync(Guid workflowId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Agentic AI client is disabled for Prompt 01. Workflow {WorkflowId} was not executed.", workflowId);
        throw new NotSupportedException("Agentic AI execution is intentionally disabled in Prompt 01.");
    }
}
