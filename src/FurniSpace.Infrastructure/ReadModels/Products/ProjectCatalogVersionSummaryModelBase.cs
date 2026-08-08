using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Products;

public abstract class ProjectCatalogVersionSummaryModelBase
{
    public Guid ProductVersionId { get; set; }
    public string VersionCode { get; set; } = string.Empty;
    public string VersionName { get; set; } = string.Empty;
    public ProductVersionType? VersionType { get; set; }
    public string? Material { get; set; }
    public string? Color { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public decimal? Depth { get; set; }
    public string? DimensionUnit { get; set; }
    public decimal? EstimatedPrice { get; set; }
    public bool? IsProjectSpecific { get; set; }
}
