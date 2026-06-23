namespace FurniSpace.Application.DTOs.Products;

public sealed class ProductPreviewImageListResponseDto
{
    public Guid ProductId { get; init; }
    public IReadOnlyList<ProductPreviewImageDto> Items { get; init; } = [];
}
