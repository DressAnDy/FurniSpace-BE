using FurniSpace.Infrastructure.DTOs.Products;

namespace FurniSpace.Application.DTOs.Products;

public sealed class ProductVersionSummaryDto : ProductVersionModelBase
{
    public CatalogFileDto? Thumbnail { get; set; }
    public IReadOnlyList<CatalogFileDto> Files { get; set; } = [];
}
