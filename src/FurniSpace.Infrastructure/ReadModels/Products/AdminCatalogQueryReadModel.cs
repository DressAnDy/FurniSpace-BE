using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Products;

public class AdminCatalogQueryReadModel
{
    public string? Keyword { get; set; }
    public Guid? CategoryId { get; set; }
    public int? BusinessTypeId { get; set; }
    public ProductStatus? ProductStatus { get; set; }
    public ProductStatus? VersionStatus { get; set; }
    public ProductVersionType? VersionType { get; set; }
    public bool? HasActiveVersion { get; set; }
    public bool? Has3DModel { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
}
