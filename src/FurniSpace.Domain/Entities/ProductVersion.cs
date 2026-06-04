using System;

namespace FurniSpace.Domain.Entities;

public class ProductVersion
{
    public Guid ProductVersionId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProjectId { get; set; }
    public string? VersionCode { get; set; }
    public string VersionName { get; set; } = null!;
    public string? VersionType { get; set; }
    public string? Material { get; set; }
    public string? Color { get; set; }
    public string? Finish { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public decimal? Depth { get; set; }
    public decimal? EstimatedPrice { get; set; }
    public string? ProductionNote { get; set; }
    public string? TechnicalNote { get; set; }
    public bool? IsDefault { get; set; }
    public bool? IsPublic { get; set; }
    public bool? IsProjectSpecific { get; set; }
    public string? Status { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}


