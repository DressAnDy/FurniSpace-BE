using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public abstract class ProductVersionModelBase
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
    public decimal? EstimatedPrice { get; set; }
    public bool? IsDefault { get; set; }
    public bool? IsPublic { get; set; }
    public bool? IsProjectSpecific { get; set; }
    public ProductStatus? Status { get; set; }
}
