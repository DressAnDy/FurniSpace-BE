using FurniSpace.Application.DTOs.Products;
using FurniSpace.Infrastructure.ReadModels.Products;

namespace FurniSpace.Application.DTOs.ProductVersions;

public sealed class ProductVersionDetailDto : ProductVersionDetailModelBase
{
    public IReadOnlyList<CatalogFileDto> Files { get; set; } = [];
}
