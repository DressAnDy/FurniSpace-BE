using FurniSpace.Application.DTOs.Products;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;

namespace FurniSpace.Application.Common;

internal static class CatalogFileUploadValidation
{
    public static List<string> ValidateGeneralUpload(
        UploadCatalogFileRequestDto request,
        FileUploadSettings uploadSettings,
        FirebaseStorageSettings firebaseSettings,
        IReadOnlySet<FileType> allowedFileTypes)
    {
        var errors = new List<string>();
        if (request.Content == Stream.Null || !request.Content.CanRead)
        {
            errors.Add("File is required.");
        }

        if (string.IsNullOrWhiteSpace(request.OriginalFileName))
        {
            errors.Add("Original file name is required.");
        }

        if (request.FileSizeBytes <= 0)
        {
            errors.Add("File size must be greater than zero.");
        }

        var maxFileSize = ResolveMaxFileSize(uploadSettings, firebaseSettings);
        if (request.FileSizeBytes > maxFileSize)
        {
            errors.Add($"File size must not exceed {maxFileSize} bytes.");
        }

        if (!allowedFileTypes.Contains(request.FileType))
        {
            errors.Add("File type is not allowed for this upload.");
        }

        var extension = Path.GetExtension(request.OriginalFileName);
        if (string.IsNullOrWhiteSpace(extension) ||
            !ResolveAllowedExtensions(uploadSettings).Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("File extension is not allowed.");
        }

        var contentType = CatalogFileStorageHelpers.NormalizeContentType(request.ContentType);
        if (!ResolveAllowedMimeTypes(uploadSettings).Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("File MIME type is not allowed.");
        }

        return errors;
    }

    public static long ResolveMaxFileSize(
        FileUploadSettings uploadSettings,
        FirebaseStorageSettings firebaseSettings)
    {
        return uploadSettings.MaxFileSizeBytes > 0
            ? uploadSettings.MaxFileSizeBytes
            : firebaseSettings.MaxFileSizeBytes;
    }

    public static string[] ResolveAllowedExtensions(FileUploadSettings uploadSettings)
    {
        return uploadSettings.AllowedExtensions.Length == 0
            ? new FileUploadSettings().AllowedExtensions
            : uploadSettings.AllowedExtensions;
    }

    public static string[] ResolveAllowedMimeTypes(FileUploadSettings uploadSettings)
    {
        return uploadSettings.AllowedMimeTypes.Length == 0
            ? new FileUploadSettings().AllowedMimeTypes
            : uploadSettings.AllowedMimeTypes;
    }
}
