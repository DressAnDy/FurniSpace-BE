using System;

namespace FurniSpace.Domain.Entities;

public class StoredFile
{
    public Guid FileId { get; set; }
    public string FileName { get; set; } = null!;
    public string FileUrl { get; set; } = null!;
    public string? MimeType { get; set; }
    public long? FileSizeBytes { get; set; }
    public Guid? UploadedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}


