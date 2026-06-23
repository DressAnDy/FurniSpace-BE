#nullable enable

using FurniSpace.Application.DTOs.Products;
using Microsoft.AspNetCore.Http;

namespace FurniSpace.API.DTOs.Products;

public sealed class UploadProductPreviewImageFormRequest
{
    public IFormFile? File { get; set; }
    public string? Description { get; set; }
    public int? DisplayOrder { get; set; }

    public UploadProductPreviewImageRequestDto ToRequestDto()
    {
        return new UploadProductPreviewImageRequestDto
        {
            Content = File?.OpenReadStream() ?? Stream.Null,
            OriginalFileName = File?.FileName ?? string.Empty,
            ContentType = File?.ContentType ?? "application/octet-stream",
            FileSizeBytes = File?.Length ?? 0,
            Description = Description,
            DisplayOrder = DisplayOrder
        };
    }
}
