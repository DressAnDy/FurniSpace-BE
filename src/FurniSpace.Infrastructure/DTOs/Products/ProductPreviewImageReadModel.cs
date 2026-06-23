using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.DTOs.Products;

public sealed class ProductPreviewImageReadModel
{
    public Guid FileId { get; set; }
    public Guid FileLinkId { get; set; }
    public Guid ProductId { get; set; }
    public FileType FileType { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int DisplayOrder { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public string StoragePath { get; set; } = string.Empty;
}
