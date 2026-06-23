namespace FurniSpace.Application.DTOs.Products;

public sealed class ReorderProductPreviewImagesRequestDto
{
    public IReadOnlyList<Guid>? FileIds { get; init; }
}
