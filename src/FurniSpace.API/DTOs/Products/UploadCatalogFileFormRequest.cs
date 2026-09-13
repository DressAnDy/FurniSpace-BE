#nullable enable

using FurniSpace.Application.DTOs.Products;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace FurniSpace.API.DTOs.Products;

public sealed class UploadCatalogFileFormRequest
{
    public IFormFile? File { get; set; }
    public FileType FileType { get; set; } = FileType.OTHER;
    public FileVisibility? Visibility { get; set; }
    public string? Description { get; set; }
    public int? DisplayOrder { get; set; }

    public UploadCatalogFileRequestDto ToRequestDto()
    {
        return new UploadCatalogFileRequestDto
        {
            Content = File?.OpenReadStream() ?? Stream.Null,
            OriginalFileName = File?.FileName ?? string.Empty,
            ContentType = File?.ContentType ?? "application/octet-stream",
            FileSizeBytes = File?.Length ?? 0,
            FileType = FileType,
            Visibility = Visibility,
            Description = Description,
            DisplayOrder = DisplayOrder
        };
    }
}
