using System.Net;
using System.Text.Json;
using AgriAssist.Api.Services.Shared;

namespace AgriAssist.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            if (exception is ApiException apiException)
            {
                await WriteProblemAsync(context, apiException.StatusCode, apiException.Code, apiException.Message);
                return;
            }

            logger.LogError(exception, "Unhandled API exception");
            await WriteProblemAsync(context, HttpStatusCode.InternalServerError, "SERVER_ERROR", "An unexpected error occurred.");
        }
    }

    internal static async Task WriteProblemAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string code,
        string message,
        TimeSpan? retryAfter = null)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";
        if (retryAfter is not null)
        {
            context.Response.Headers.RetryAfter =
                Math.Ceiling(retryAfter.Value.TotalSeconds)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var payload = new
        {
            error = new
            {
                code,
                message,
                traceId = context.TraceIdentifier
            }
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
