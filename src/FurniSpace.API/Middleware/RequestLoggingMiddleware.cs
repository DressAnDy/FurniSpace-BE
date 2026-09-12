#nullable enable

using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using FurniSpace.API.Logging;
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

        var summary = RequestLogSummary.Get(context);
        Log.ForContext<RequestLoggingMiddleware>()
            .ForContext("EventType", "HttpRequestCompleted")
            .ForContext("UserId", userId)
            .ForContext("StatusCode", statusCode)
            .ForContext("ErrorCode", summary?.ErrorCode)
            .ForContext("ElapsedMs", elapsedMilliseconds)
            .Write(
                level,
                "{RequestLog:l}",
                FormatRequestLog(
                    context.Request.Method,
                    context.Request.Path.Value,
                    statusCode,
                    summary?.ErrorCode,
                    summary?.Message,
                    elapsedMilliseconds));
    }

    internal static string FormatRequestLog(
        string method,
        string? path,
        int statusCode,
        string? errorCode,
        string? message,
        double elapsedMilliseconds)
    {
        var code = string.IsNullOrWhiteSpace(errorCode)
            ? statusCode.ToString(CultureInfo.InvariantCulture)
            : $"{statusCode.ToString(CultureInfo.InvariantCulture)} {errorCode}";
        var builder = new StringBuilder()
            .Append("API      ").Append(method).Append(' ').Append(path)
            .AppendLine()
            .Append("               Code     ").Append(code);

        if (!string.IsNullOrWhiteSpace(message))
        {
            builder.AppendLine().Append("               Message  ").Append(message);
        }

        return builder
            .AppendLine()
            .Append("               Time     ")
            .Append(elapsedMilliseconds.ToString("0", CultureInfo.InvariantCulture))
            .Append("ms")
            .ToString();
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
