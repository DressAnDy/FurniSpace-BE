using System;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Domain.Entities;

public class StoredFile
{
    public Guid FileId { get; set; }
    public Guid UploadedBy { get; set; }
    public string OriginalFileName { get; set; } = null!;
    public string StoredFileName { get; set; } = null!;
    public string FileUrl { get; set; } = null!;
    public string StoragePath { get; set; } = null!;
    public string MimeType { get; set; } = null!;
    public string? FileExtension { get; set; }
    public long FileSizeBytes { get; set; }
    public string? Checksum { get; set; }
    public FileStatus? Status { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
}
