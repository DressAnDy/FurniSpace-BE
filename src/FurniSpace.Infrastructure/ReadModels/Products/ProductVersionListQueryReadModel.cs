using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Products;

public sealed class ProductVersionListQueryReadModel
{
    public Guid ProductId { get; set; }
    public ProductStatus? Status { get; set; }
    public ProductVersionType? VersionType { get; set; }
    public bool? IsDefault { get; set; }
    public bool? IsPublic { get; set; }
    public bool? IsProjectSpecific { get; set; }
    public Guid? ProjectId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
