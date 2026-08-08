using FurniSpace.Infrastructure.ReadModels.Products;

namespace FurniSpace.Application.DTOs.Catalog;

public sealed class ProductVersionManagementDto : ProductVersionModelBase
{
    public Guid ProductId { get; set; }
    public Guid? ProjectId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
