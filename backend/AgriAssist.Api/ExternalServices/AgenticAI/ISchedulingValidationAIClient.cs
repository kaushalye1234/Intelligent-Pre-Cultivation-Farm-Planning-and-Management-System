using AgriAssist.Api.Dtos.TaskApproval;

namespace AgriAssist.Api.ExternalServices.AgenticAI;

public interface ISchedulingValidationAIClient
{
    Task<SchedulingValidationOutput> RunSchedulingValidationAsync(SchedulingValidationInput input, CancellationToken cancellationToken);
}
