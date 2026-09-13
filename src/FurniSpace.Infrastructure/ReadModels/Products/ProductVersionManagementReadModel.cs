namespace FurniSpace.Infrastructure.ReadModels.Products;

public class ProductVersionManagementReadModel : ProductVersionModelBase
{
    public Guid ProductId { get; set; }
    public Guid? ProjectId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
