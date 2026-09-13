using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Products;

public sealed class ProductPreviewImageDto
{
    public Guid FileId { get; init; }
    public string Url { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
    public FileType FileType { get; init; }
    public string? Description { get; init; }
    public string MimeType { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public bool IsCover { get; init; }
    public DateTime CreatedAt { get; init; }
}
