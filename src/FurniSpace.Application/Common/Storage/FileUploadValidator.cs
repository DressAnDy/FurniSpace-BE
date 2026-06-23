using FurniSpace.Infrastructure.Common.Storage;
using Microsoft.Extensions.Options;

namespace FurniSpace.Application.Common.Storage;

public sealed class FileUploadValidator : IFileUploadValidator
{
    private readonly FileUploadSettings _uploadSettings;
    private readonly FirebaseStorageSettings _firebaseSettings;

    public FileUploadValidator(
        IOptions<FileUploadSettings> uploadSettings,
        IOptions<FirebaseStorageSettings> firebaseSettings)
    {
        _uploadSettings = uploadSettings.Value;
        _firebaseSettings = firebaseSettings.Value;
    }

    public FileUploadValidationResult Validate(IFileUploadPayload payload)
    {
        if (payload.Content == Stream.Null || !payload.Content.CanRead)
        {
            return FileUploadValidationResult.Failure(
                FileUploadValidationFailureKind.MissingFile,
                "File is required.");
        }

        if (string.IsNullOrWhiteSpace(payload.OriginalFileName))
        {
            return FileUploadValidationResult.Failure(
                FileUploadValidationFailureKind.MissingFileName,
                "Original file name is required.");
        }

        if (payload.FileSizeBytes <= 0)
        {
            return FileUploadValidationResult.Failure(
                FileUploadValidationFailureKind.InvalidFileSize,
                "File size must be greater than zero.");
        }

        var maxFileSize = ResolveMaxFileSize();
        if (payload.FileSizeBytes > maxFileSize)
        {
            return FileUploadValidationResult.Failure(
                FileUploadValidationFailureKind.FileTooLarge,
                $"File size must not exceed {maxFileSize} bytes.");
        }

        var extension = Path.GetExtension(payload.OriginalFileName);
        if (string.IsNullOrWhiteSpace(extension) ||
            !AllowedExtensions().Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return FileUploadValidationResult.Failure(
                FileUploadValidationFailureKind.InvalidExtension,
                "File extension is not allowed.");
        }

        var contentType = ProjectFileUploadSupport.NormalizeContentType(payload.ContentType);
        if (!AllowedMimeTypes().Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return FileUploadValidationResult.Failure(
                FileUploadValidationFailureKind.InvalidMimeType,
                "File MIME type is not allowed.");
        }

        return FileUploadValidationResult.Success();
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
