namespace AgriAssist.Api.ExternalServices.AgenticAI;

public interface IAgenticAIClient
{
    Task StartWorkflowAsync(Guid workflowId, CancellationToken cancellationToken);
}
