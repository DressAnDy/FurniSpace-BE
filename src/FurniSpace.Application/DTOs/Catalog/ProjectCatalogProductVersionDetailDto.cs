using FurniSpace.Application.DTOs.Products;
using FurniSpace.Infrastructure.ReadModels.Products;

namespace FurniSpace.Application.DTOs.Catalog;

public sealed class ProjectCatalogProductVersionDetailDto : ProjectCatalogEligibleVersionReadModel
{
    public IReadOnlyList<CatalogFileDto> Files { get; set; } = [];
}
