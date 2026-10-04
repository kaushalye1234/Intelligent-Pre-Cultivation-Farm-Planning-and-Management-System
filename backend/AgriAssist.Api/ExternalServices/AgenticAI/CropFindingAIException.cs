using System.Net;
using AgriAssist.Api.Dtos.CropPlanning;

namespace AgriAssist.Api.ExternalServices.AgenticAI;

public sealed class CropFindingAIException(
    HttpStatusCode responseStatusCode,
    CropFindingErrorDetail? detail) : HttpRequestException(
        detail?.Message ?? "The AI service returned an invalid CropFinding error response.",
        null,
        responseStatusCode)
{
    public HttpStatusCode ResponseStatusCode { get; } = responseStatusCode;
    public CropFindingErrorDetail? Detail { get; } = detail;
}
