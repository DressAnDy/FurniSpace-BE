using FurniSpace.Application.DTOs.Products;

namespace FurniSpace.Application.DTOs.Catalog;

public sealed class ProjectCatalogProductDetailDto
{
    public Guid ProductId { get; set; }
    public string? ProductCode { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public int[]? BusinessTypeIds { get; set; }
    public CatalogFileDto? Thumbnail { get; set; }
    public IReadOnlyList<CatalogFileDto> Files { get; set; } = [];
    public IReadOnlyList<ProjectCatalogVersionSummaryDto> EligibleVersions { get; set; } = [];
}
