using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.DTOs.ProjectFiles;

public sealed class FileMetadataReadModel
{
    public Guid FileId { get; init; }
    public Guid? FileLinkId { get; init; }
    public string ReferenceType { get; init; } = string.Empty;
    public Guid? ReferenceId { get; init; }
    public string OriginalFileName { get; init; } = string.Empty;
    public string StoredFileName { get; init; } = string.Empty;
    public FileType? FileType { get; init; }
    public string MimeType { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public string StoragePath { get; init; } = string.Empty;
    public string FileUrl { get; init; } = string.Empty;
    public FileVisibility? Visibility { get; init; }
    public Guid UploadedBy { get; init; }
    public DateTime UploadedAt { get; init; }
    public FileStatus? Status { get; init; }
    public int? DisplayOrder { get; init; }
    public bool? IsPrimary { get; init; }
    public ProjectFileAccessReadModel? ProjectAccess { get; init; }
}
