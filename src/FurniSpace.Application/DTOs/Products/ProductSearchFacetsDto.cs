using FurniSpace.Application.DTOs.Search;

namespace FurniSpace.Application.DTOs.Products;

public sealed class ProductSearchFacetsDto
{
    public IReadOnlyList<SearchFacetItemDto> Categories { get; set; } = [];

    public IReadOnlyList<SearchFacetItemDto> Materials { get; set; } = [];

    public IReadOnlyList<SearchFacetItemDto> Colors { get; set; } = [];
}
