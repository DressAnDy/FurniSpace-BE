#nullable enable

using FurniSpace.Application.Common;

namespace FurniSpace.API.Logging;

internal sealed record RequestLogSummary(string? Message, string? ErrorCode)
{
    public const string ItemKey = "RequestLogSummary";
    private const int MaxMessageLength = 180;

    public static void Set(HttpContext context, string? message, string? errorCode)
    {
        context.Items[ItemKey] = new RequestLogSummary(Normalize(message), Normalize(errorCode));
    }

    public static void Set(HttpContext context, IServiceResult result)
    {
        Set(context, BuildMessage(result), result.ErrorCode);
    }

    public static RequestLogSummary? Get(HttpContext context)
    {
        return context.Items[ItemKey] as RequestLogSummary;
    }

    private static string? BuildMessage(IServiceResult result)
    {
        if (result.Errors is not { Count: > 0 })
        {
            return result.Message;
        }

        var details = string.Join("; ", result.Errors.Where(error => !string.IsNullOrWhiteSpace(error)).Take(3));
        return string.IsNullOrWhiteSpace(result.Message)
            ? details
            : $"{result.Message}: {details}";
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = string.Join(' ', value.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length <= MaxMessageLength
            ? normalized
            : $"{normalized[..(MaxMessageLength - 3)]}...";
    }
}
