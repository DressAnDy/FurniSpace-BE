using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Application.Common;

internal static class DatabaseExceptionMapper
{
    internal static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        var message = GetInnermostMessage(exception);
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsVersionCodeUniqueViolation(DbUpdateException exception)
    {
        var message = GetInnermostMessage(exception);
        return !string.IsNullOrWhiteSpace(message) &&
               message.Contains("version_code", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsCustomizationVersionNumberUniqueViolation(DbUpdateException exception)
    {
        var message = GetInnermostMessage(exception);
        return !string.IsNullOrWhiteSpace(message) &&
               (message.Contains("customization_request_id", StringComparison.OrdinalIgnoreCase) &&
                message.Contains("version_no", StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsFileLinkUniqueViolation(DbUpdateException exception)
    {
        var message = GetInnermostMessage(exception);
        return !string.IsNullOrWhiteSpace(message) &&
               message.Contains("file_links", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsProjectShowcaseCoverUniqueViolation(DbUpdateException exception)
    {
        var message = GetInnermostMessage(exception);
        return !string.IsNullOrWhiteSpace(message) &&
               message.Contains("ux_project_showcase_media_one_cover", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetInnermostMessage(Exception exception)
    {
        var current = exception;
        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }

        return current.Message;
    }
}
