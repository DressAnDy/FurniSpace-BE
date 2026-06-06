namespace FurniSpace.Application.DTOs.Products;

public sealed class ProductListResponseDto
{
    public IReadOnlyList<ProductListItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int Limit { get; set; }
    public int Total { get; set; }
}
