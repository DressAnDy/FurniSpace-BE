using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.ProjectFiles;

public static class ProjectFileDirectUploadErrorCodes
{
    public const string UploadNotFound = "PROJECT_FILE_UPLOAD_NOT_FOUND";
    public const string UploadNotPending = "PROJECT_FILE_UPLOAD_NOT_PENDING";
    public const string UploadForbidden = "PROJECT_FILE_UPLOAD_FORBIDDEN";
    public const string UploadObjectMissing = "PROJECT_FILE_UPLOAD_OBJECT_MISSING";
    public const string UploadSizeMismatch = "PROJECT_FILE_UPLOAD_SIZE_MISMATCH";
    public const string UploadContentTypeMismatch = "PROJECT_FILE_UPLOAD_CONTENT_TYPE_MISMATCH";
}

public sealed class PrepareProjectFileUploadRequestDto
{
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long FileSizeBytes { get; set; }
    public FileType FileType { get; set; } = FileType.OTHER;
    public FileVisibility? Visibility { get; set; }
    public bool? IsPrimary { get; set; }
    public int? DisplayOrder { get; set; }
    public string? Note { get; set; }
}

public sealed class PrepareProjectFileUploadResponseDto
{
    public Guid FileId { get; set; }
    public Guid ProjectId { get; set; }
    public string UploadUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public sealed class CompleteProjectFileUploadRequestDto
{
    public Guid FileId { get; set; }
}
