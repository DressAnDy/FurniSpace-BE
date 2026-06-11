using FurniSpace.Application.DTOs.Products;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace FurniSpace.Application.Common.Catalog;

public sealed class CatalogFileUploadRules
{
    private readonly FileUploadSettings _uploadSettings;
    private readonly FirebaseStorageSettings _firebaseSettings;

    public CatalogFileUploadRules(
        IOptions<FileUploadSettings> uploadSettings,
        IOptions<FirebaseStorageSettings> firebaseSettings)
    {
        _uploadSettings = uploadSettings.Value;
        _firebaseSettings = firebaseSettings.Value;
    }

    public List<string> Validate(UploadCatalogFileRequestDto request, HashSet<FileType> allowedFileTypes)
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

        var maxFileSize = ResolveMaxFileSize();
        if (request.FileSizeBytes > maxFileSize)
        {
            errors.Add($"File size must not exceed {maxFileSize} bytes.");
        }

        if (!allowedFileTypes.Contains(request.FileType))
        {
            errors.Add("File type is not allowed for this upload.");
        }

        var extension = Path.GetExtension(request.OriginalFileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions().Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("File extension is not allowed.");
        }

        var contentType = NormalizeContentType(request.ContentType);
        if (!AllowedMimeTypes().Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("File MIME type is not allowed.");
        }

        return errors;
    }

    public string BuildObjectName(string defaultPrefix, string? configuredPrefix, Guid referenceId, string generatedFileName)
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

    private long ResolveMaxFileSize()
    {
        return _uploadSettings.MaxFileSizeBytes > 0
            ? _uploadSettings.MaxFileSizeBytes
            : _firebaseSettings.MaxFileSizeBytes;
    }

    private string[] AllowedExtensions()
    {
        return _uploadSettings.AllowedExtensions.Length == 0
            ? new FileUploadSettings().AllowedExtensions
            : _uploadSettings.AllowedExtensions;
    }

    private string[] AllowedMimeTypes()
    {
        return _uploadSettings.AllowedMimeTypes.Length == 0
            ? new FileUploadSettings().AllowedMimeTypes
            : _uploadSettings.AllowedMimeTypes;
    }
}
