using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Products;

public sealed class ProjectCatalogQueryReadModel
{
    public Guid ProjectId { get; set; }
    public string? Keyword { get; set; }
    public Guid? CategoryId { get; set; }
    public int? BusinessTypeId { get; set; }
    public ProductVersionType? VersionType { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
