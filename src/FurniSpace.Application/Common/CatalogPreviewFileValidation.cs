using FurniSpace.Infrastructure.Common.Storage;

namespace FurniSpace.Application.Common;

internal static class CatalogPreviewFileValidation
{
    public static ProductPreviewImageSettings ResolveEffectiveSettings(ProductPreviewImageSettings configured)
    {
        var defaults = ProductPreviewImageSettings.CreateDefault();
        return new ProductPreviewImageSettings
        {
            MaxCount = configured.MaxCount > 0 ? configured.MaxCount : defaults.MaxCount,
            MaxFileSizeBytes = configured.MaxFileSizeBytes > 0
                ? configured.MaxFileSizeBytes
                : defaults.MaxFileSizeBytes,
            AllowedExtensions = configured.AllowedExtensions.Length == 0
                ? defaults.AllowedExtensions
                : configured.AllowedExtensions,
            AllowedMimeTypes = configured.AllowedMimeTypes.Length == 0
                ? defaults.AllowedMimeTypes
                : configured.AllowedMimeTypes
        };
    }

    public static Error? ValidateFileContent(
        Stream content,
        string originalFileName,
        string? contentType,
        long fileSizeBytes,
        ProductPreviewImageSettings settings,
        string invalidFileTypeCode,
        string fileTooLargeCode)
    {
        var effectiveSettings = ResolveEffectiveSettings(settings);

        if (content == Stream.Null || !content.CanRead)
        {
            return Error.BadRequest(invalidFileTypeCode, "File is required.");
        }

        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            return Error.BadRequest(invalidFileTypeCode, "Original file name is required.");
        }

        if (fileSizeBytes <= 0)
        {
            return Error.BadRequest(invalidFileTypeCode, "File size must be greater than zero.");
        }

        if (fileSizeBytes > effectiveSettings.MaxFileSizeBytes)
        {
            return Error.PayloadTooLarge(
                fileTooLargeCode,
                $"File size must not exceed {effectiveSettings.MaxFileSizeBytes} bytes.");
        }

        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension) ||
            !effectiveSettings.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return Error.UnsupportedMediaType(
                invalidFileTypeCode,
                "File extension is not allowed for product preview images.");
        }

        var normalizedContentType = NormalizeContentType(contentType);
        if (!effectiveSettings.AllowedMimeTypes.Contains(normalizedContentType, StringComparer.OrdinalIgnoreCase))
        {
            return Error.UnsupportedMediaType(
                invalidFileTypeCode,
                "File MIME type is not allowed for product preview images.");
        }

        return null;
    }

    public static string NormalizeContentType(string? contentType)
    {
        return string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType.Trim();
    }
}
