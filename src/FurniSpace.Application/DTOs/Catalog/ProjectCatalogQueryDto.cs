using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Catalog;

public sealed class ProjectCatalogQueryDto
{
    public string? Keyword { get; set; }
    public Guid? CategoryId { get; set; }
    public int? BusinessTypeId { get; set; }
    public ProductVersionType? VersionType { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
