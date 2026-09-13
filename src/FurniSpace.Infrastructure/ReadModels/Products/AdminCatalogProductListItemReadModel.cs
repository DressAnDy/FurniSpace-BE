using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Products;

public class AdminCatalogProductListItemReadModel : AdminCatalogProductSummaryModelBase
{
    public Guid? DefaultVersionId { get; set; }
    public string? DefaultVersionCode { get; set; }
    public string? DefaultVersionName { get; set; }
    public ProductStatus? DefaultVersionStatus { get; set; }
    public decimal? DefaultVersionEstimatedPrice { get; set; }
}
