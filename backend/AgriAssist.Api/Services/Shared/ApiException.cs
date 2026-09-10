using System.Net;

namespace AgriAssist.Api.Services.Shared;

public sealed class ApiException(HttpStatusCode statusCode, string code, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}
