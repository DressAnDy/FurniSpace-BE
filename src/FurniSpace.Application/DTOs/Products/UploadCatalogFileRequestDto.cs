using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Products;

public sealed class UploadCatalogFileRequestDto
{
    public Stream Content { get; init; } = Stream.Null;
    public string OriginalFileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
    public long FileSizeBytes { get; init; }
    public FileType FileType { get; init; } = FileType.OTHER;
    public FileVisibility? Visibility { get; init; }
    public string? Description { get; init; }
}
