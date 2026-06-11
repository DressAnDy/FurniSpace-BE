using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Products;

public sealed class CatalogFileDto
{
    public Guid FileId { get; set; }
    public Guid FileLinkId { get; set; }
    public FileType? FileType { get; set; }
    public string? OriginalFileName { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long? FileSizeBytes { get; set; }
}
