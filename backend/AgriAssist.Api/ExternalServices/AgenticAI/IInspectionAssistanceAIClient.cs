using AgriAssist.Api.Dtos.CropPlanning;

namespace AgriAssist.Api.ExternalServices.AgenticAI;

public interface IInspectionAssistanceAIClient
{
    Task<InspectionNoteAssistanceResponse> GenerateNoteSuggestionsAsync(
        InspectionNoteAssistanceAiInput input,
        CancellationToken cancellationToken);
}

