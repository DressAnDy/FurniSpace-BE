namespace FurniSpace.Application.DTOs.Products;

public sealed class ProductSearchRequestDto
{
    public string? Query { get; init; }

    public Guid? CategoryId { get; init; }

    public string? Material { get; init; }

    public string? Color { get; init; }

    public decimal? MinPrice { get; init; }

    public decimal? MaxPrice { get; init; }

    public string? Sort { get; init; }

    public int Page { get; init; } = 1;

    public int Limit { get; init; } = 20;
}
