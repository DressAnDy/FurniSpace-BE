namespace FurniSpace.Application.DTOs.Catalog;

public sealed class ProjectCatalogListResponseDto
{
    public IReadOnlyList<ProjectCatalogProductItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}
