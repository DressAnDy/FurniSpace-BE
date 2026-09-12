using System.Diagnostics;
using System.Security.Claims;
using Serilog;
using Serilog.Context;
using Serilog.Events;

namespace FurniSpace.API.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next)
{
    private const long SlowRequestMilliseconds = 1_000;
    private const long NotableReadMilliseconds = 300;

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
        var elapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
        var level = GetLogLevel(
            context.Request.Method,
            context.Request.Path,
            statusCode,
            (long)elapsedMilliseconds);

        Log.ForContext<RequestLoggingMiddleware>()
            .ForContext("EventType", "HttpRequestCompleted")
            .ForContext("UserId", userId)
            .Write(
                level,
                "{RequestMethod} {RequestPath} {StatusCode} {ElapsedMs:0}ms",
                context.Request.Method,
                context.Request.Path.Value,
                statusCode,
                elapsedMilliseconds);
    }

    internal static LogEventLevel GetLogLevel(
        string method,
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

        if (elapsedMilliseconds >= SlowRequestMilliseconds)
        {
            return LogEventLevel.Warning;
        }

        if (requestPath.StartsWithSegments(LoginPath)
            || (IsRoutineRead(method) && elapsedMilliseconds < NotableReadMilliseconds))
        {
            return LogEventLevel.Debug;
        }

        return LogEventLevel.Information;
    }

    private static bool IsRoutineRead(string method)
    {
        return HttpMethods.IsGet(method)
            || HttpMethods.IsHead(method)
            || HttpMethods.IsOptions(method);
    }
}
