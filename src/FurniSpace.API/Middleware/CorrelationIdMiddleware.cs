using System.Diagnostics;
using Serilog.Context;

namespace FurniSpace.API.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";

    private const int MaxCorrelationIdLength = 128;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetCorrelationId(context);
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TraceId", traceId))
        {
            await next(context);
        }
    }

    private static string GetCorrelationId(HttpContext context)
    {
        var requestedCorrelationId = context.Request.Headers[HeaderName].FirstOrDefault();

        return !string.IsNullOrWhiteSpace(requestedCorrelationId)
            && requestedCorrelationId.Length <= MaxCorrelationIdLength
            && requestedCorrelationId.All(character =>
                char.IsLetterOrDigit(character) || character is '-' or '_' or '.')
                ? requestedCorrelationId
                : Guid.NewGuid().ToString("N");
    }
}
