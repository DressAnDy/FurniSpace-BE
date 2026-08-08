using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Catalog;

public sealed class AdminCatalogDefaultVersionSummaryDto
{
    public Guid ProductVersionId { get; set; }
    public string VersionCode { get; set; } = string.Empty;
    public string VersionName { get; set; } = string.Empty;
    public ProductStatus? Status { get; set; }
    public decimal? EstimatedPrice { get; set; }
    public decimal? DefaultTaxRate { get; set; }
}
