using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.ProjectFiles;

public sealed class FileDetailResponseDto
{
    public Guid FileId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public FileType? FileType { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
    public Guid UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; }
    public FileStatus? Status { get; set; }
}
