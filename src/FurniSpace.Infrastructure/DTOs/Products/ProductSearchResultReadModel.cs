namespace FurniSpace.Infrastructure.DTOs.Products;

public sealed class ProductSearchResultReadModel
{
    public IReadOnlyList<ProductListItemReadModel> Items { get; init; } = [];

    public int Total { get; init; }
}
