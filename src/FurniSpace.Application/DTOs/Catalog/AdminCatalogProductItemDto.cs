using FurniSpace.Infrastructure.ReadModels.Products;

namespace FurniSpace.Application.DTOs.Catalog;

public class AdminCatalogProductItemDto : AdminCatalogProductSummaryModelBase
{
    public AdminCatalogDefaultVersionSummaryDto? DefaultVersionSummary { get; set; }
}
