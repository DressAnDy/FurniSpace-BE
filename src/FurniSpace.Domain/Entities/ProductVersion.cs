using System;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Domain.Entities;

public class ProductVersion
{
    public Guid ProductVersionId { get; set; }
    public Guid ProductId { get; set; }
    public string VersionCode { get; set; } = null!;
    public string VersionName { get; set; } = null!;
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
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
