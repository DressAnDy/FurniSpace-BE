using FurniSpace.Application.DTOs.Products;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.ProductVersions;

public sealed class ProductVersionDetailDto
{
    public Guid ProductVersionId { get; set; }
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
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
    public IReadOnlyList<CatalogFileDto> Files { get; set; } = [];
}
