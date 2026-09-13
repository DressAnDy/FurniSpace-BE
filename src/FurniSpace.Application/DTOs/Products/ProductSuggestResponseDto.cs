namespace FurniSpace.Application.DTOs.Products;

public sealed class ProductSuggestItemDto
{
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;
}

public sealed class ProductSuggestResponseDto
{
    public IReadOnlyList<ProductSuggestItemDto> Items { get; set; } = [];
}
