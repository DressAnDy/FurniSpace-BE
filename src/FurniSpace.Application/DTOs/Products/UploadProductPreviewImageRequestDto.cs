namespace FurniSpace.Application.DTOs.Products;

public sealed class UploadProductPreviewImageRequestDto
{
    public Stream Content { get; init; } = Stream.Null;
    public string OriginalFileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
    public long FileSizeBytes { get; init; }
    public string? Description { get; init; }
    public int? DisplayOrder { get; init; }
}
