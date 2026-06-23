namespace FurniSpace.Application.Common;

internal static class CatalogFileStorageHelpers
{
    public static string BuildStorageObjectName(
        string defaultPrefix,
        string? configuredPrefix,
        Guid referenceId,
        string generatedFileName)
    {
        var prefix = string.IsNullOrWhiteSpace(configuredPrefix)
            ? defaultPrefix
            : configuredPrefix.Trim().Trim('/');

        return $"{prefix}/{referenceId:D}/{generatedFileName}";
    }

    public static string BuildGeneratedFileName(Guid fileId, string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        return $"{fileId:N}{extension}";
    }

    public static string? NormalizeExtension(string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        return extension.TrimStart('.').ToLowerInvariant();
    }

    public static string NormalizeContentType(string? contentType)
    {
        return string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType.Trim();
    }

    public static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static string NormalizeOriginalFileName(string originalFileName)
    {
        return Path.GetFileName(originalFileName.Trim());
    }
}
