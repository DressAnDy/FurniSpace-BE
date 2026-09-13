using FurniSpace.Application.Common;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common.Reports;

internal static class ReportResolutionSupport
{
    internal const int MaxResolutionNoteLength = 4000;

    internal static string? NormalizeResolutionNote(string? note)
    {
        var trimmed = note?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    internal static ServiceResult<T>? ValidateResolutionNote<T>(string? note)
    {
        var normalized = NormalizeResolutionNote(note);
        if (normalized?.Length > MaxResolutionNoteLength)
        {
            return ServiceResult<T>.Failure(
                Error.Validation(
                    ReportResolutionErrorCodes.ResolutionNoteTooLong,
                    $"Resolution note must not exceed {MaxResolutionNoteLength} characters."));
        }

        return null;
    }

    internal static void ApplyResolveTransition(
        ReportResolutionStatus currentStatus,
        Action applyFirstResolve)
    {
        if (currentStatus == ReportResolutionStatus.RESOLVED)
        {
            return;
        }

        applyFirstResolve();
    }
}

internal static class ReportResolutionErrorCodes
{
    internal const string ResolutionNoteTooLong = "REPORT_RESOLUTION_NOTE_TOO_LONG";
    internal const string AlreadyResolved = "REPORT_ALREADY_RESOLVED";
}
