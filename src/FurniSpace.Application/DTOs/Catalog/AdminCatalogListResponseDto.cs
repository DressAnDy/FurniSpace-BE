namespace FurniSpace.Application.DTOs.Catalog;

public sealed class AdminCatalogListResponseDto
{
    public IReadOnlyList<AdminCatalogProductItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}
