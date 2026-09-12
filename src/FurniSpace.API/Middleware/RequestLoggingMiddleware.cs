using System.Diagnostics;
using System.Security.Claims;
using Serilog;
using Serilog.Context;
using Serilog.Events;

namespace FurniSpace.API.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next)
{
    private static readonly PathString LoginPath = new("/auth/login");
    private static readonly PathString HubsPath = new("/hubs");

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var isLoginRequest = context.Request.Path.StartsWithSegments(LoginPath);
        var userId = isLoginRequest
            ? null
            : context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirstValue("sub");

        using (LogContext.PushProperty("RequestMethod", context.Request.Method))
        using (LogContext.PushProperty("RequestPath", context.Request.Path.Value))
        using (LogContext.PushProperty("UserId", userId))
        {
            await next(context);
        }

        stopwatch.Stop();

        var statusCode = context.Response.StatusCode;
        var level = GetLogLevel(
            context.Request.Path,
            statusCode,
            stopwatch.ElapsedMilliseconds);

        Log.ForContext<RequestLoggingMiddleware>()
            .ForContext("EventType", "HttpRequestCompleted")
            .ForContext("RequestMethod", context.Request.Method)
            .ForContext("RequestPath", context.Request.Path.Value)
            .ForContext("StatusCode", statusCode)
            .ForContext("ElapsedMs", stopwatch.Elapsed.TotalMilliseconds)
            .ForContext("UserId", userId)
            .Write(
                level,
                "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {ElapsedMs:0.000} ms",
                context.Request.Method,
                context.Request.Path.Value,
                statusCode,
                stopwatch.Elapsed.TotalMilliseconds);
    }

    internal static LogEventLevel GetLogLevel(
        PathString requestPath,
        int statusCode,
        long elapsedMilliseconds)
    {
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            return LogEventLevel.Error;
        }

        if (statusCode >= StatusCodes.Status400BadRequest)
        {
            return LogEventLevel.Warning;
        }

        if (requestPath.StartsWithSegments(HubsPath))
        {
            return LogEventLevel.Debug;
        }

        if (elapsedMilliseconds >= 1_000)
        {
            return LogEventLevel.Warning;
        }

        if (requestPath.StartsWithSegments(LoginPath))
        {
            return LogEventLevel.Debug;
        }

        return LogEventLevel.Information;
    }
}
