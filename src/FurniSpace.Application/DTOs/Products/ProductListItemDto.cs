using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Products;

public sealed class ProductListItemDto
{
    public Guid ProductId { get; set; }
    public Guid? CategoryId { get; set; }
    public int[]? BusinessTypeIds { get; set; }
    public string? CategoryName { get; set; }
    public string? ProductCode { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProductStatus? Status { get; set; }
    public CatalogFileDto? Thumbnail { get; set; }
    public IReadOnlyList<ProductBusinessTypeDto> BusinessTypes { get; set; } = [];
    public ProductVersionSummaryDto? DefaultVersion { get; set; }
}
