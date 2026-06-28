using FurniSpace.Application.DTOs.Products;
using FurniSpace.Infrastructure.ReadModels.Products;

namespace FurniSpace.Application.DTOs.ProductVersions;

public sealed class ProductVersionDto : ProductVersionModelBase
{
    public Guid ProductId { get; set; }
    public CatalogFileDto? Thumbnail { get; set; }
    public IReadOnlyList<CatalogFileDto> Files { get; set; } = [];
}
