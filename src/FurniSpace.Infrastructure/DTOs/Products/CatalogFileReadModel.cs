using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.DTOs.Products;

public sealed class CatalogFileReadModel
{
    public Guid FileId { get; set; }
    public Guid FileLinkId { get; set; }
    public Guid ReferenceId { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public FileType? FileType { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public FileVisibility? Visibility { get; set; }
    public FileStatus? Status { get; set; }
    public int? DisplayOrder { get; set; }
    public bool? IsPrimary { get; set; }
    public string? Description { get; set; }
    public DateTime UploadedAt { get; set; }
}
