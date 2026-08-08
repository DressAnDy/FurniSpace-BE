using FurniSpace.Application.DTOs.Products;

namespace FurniSpace.Application.DTOs.Catalog;

public sealed class ProjectCatalogProductItemDto
{
    public Guid ProductId { get; set; }
    public string? ProductCode { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public int[]? BusinessTypeIds { get; set; }
    public CatalogFileDto? Thumbnail { get; set; }
    public int EligibleVersionCount { get; set; }
    public IReadOnlyList<ProjectCatalogVersionSummaryDto> EligibleVersions { get; set; } = [];
}
