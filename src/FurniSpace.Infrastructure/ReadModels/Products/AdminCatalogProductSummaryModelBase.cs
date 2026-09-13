using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Products;

public abstract class AdminCatalogProductSummaryModelBase
{
    public Guid ProductId { get; set; }
    public string? ProductCode { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public int[]? BusinessTypeIds { get; set; }
    public ProductStatus? Status { get; set; }
    public int TotalVersionCount { get; set; }
    public int ActiveVersionCount { get; set; }
    public int InactiveVersionCount { get; set; }
    public int ArchivedVersionCount { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
