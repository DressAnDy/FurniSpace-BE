using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Storage;

namespace FurniSpace.Application.Common.Storage;

internal static class ProjectFileUploadSupport
{
    internal const string ProjectReferenceType = "PROJECT";

    internal static string BuildProjectObjectName(
        FirebaseStorageSettings firebaseSettings,
        Guid projectId,
        string generatedFileName)
    {
        var prefix = string.IsNullOrWhiteSpace(firebaseSettings.ProjectFilesPrefix)
            ? "projects"
            : firebaseSettings.ProjectFilesPrefix.Trim().Trim('/');

        return $"{prefix}/{projectId:D}/{generatedFileName}";
    }

    internal static string BuildGeneratedFileName(Guid fileId, string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        return $"{fileId:N}{extension}";
    }

    internal static string? NormalizeExtension(string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        return extension.TrimStart('.').ToLowerInvariant();
    }

    internal static string NormalizeContentType(string? contentType)
    {
        return string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType.Trim();
    }

    internal static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    internal static FileVisibility ResolveVisibility(
        FileVisibility? requestedVisibility,
        string? roleName,
        string customerRole)
    {
        if (requestedVisibility.HasValue)
        {
            return requestedVisibility.Value;
        }

        return string.Equals(roleName, customerRole, StringComparison.OrdinalIgnoreCase)
            ? FileVisibility.CUSTOMER_VISIBLE
            : FileVisibility.STAFF_ONLY;
    }

    internal static StoredFile CreateStoredFile(StoredFileCreationRequest request)
    {
        return new StoredFile
        {
            FileId = request.FileId,
            UploadedBy = request.UploadedBy,
            OriginalFileName = request.OriginalFileName,
            StoredFileName = request.GeneratedFileName,
            FileUrl = request.UploadResult.PublicUrl,
            StoragePath = request.UploadResult.ObjectName,
            MimeType = NormalizeContentType(request.ContentType),
            FileExtension = NormalizeExtension(request.OriginalFileName),
            FileSizeBytes = request.FileSizeBytes,
            Status = FileStatus.ACTIVE,
            UploadedAt = request.UploadedAt
        };
    }

    internal static FileLink CreateProjectFileLink(ProjectFileLinkCreationRequest request)
    {
        return new FileLink
        {
            FileLinkId = request.FileLinkId,
            FileId = request.FileId,
            ReferenceType = ProjectReferenceType,
            ReferenceId = request.ProjectId,
            FileType = request.FileType,
            Visibility = request.Visibility,
            Description = request.Description,
            CreatedBy = request.CreatedBy,
            CreatedAt = request.CreatedAt
        };
    }
}

internal sealed record StoredFileCreationRequest(
    Guid FileId,
    Guid UploadedBy,
    string OriginalFileName,
    string GeneratedFileName,
    StorageUploadResult UploadResult,
    string ContentType,
    long FileSizeBytes,
    DateTime UploadedAt);

internal sealed record ProjectFileLinkCreationRequest(
    Guid FileLinkId,
    Guid FileId,
    Guid ProjectId,
    FileType FileType,
    FileVisibility Visibility,
    string? Description,
    Guid CreatedBy,
    DateTime CreatedAt);
