namespace FurniSpace.Infrastructure.DTOs.Products;

public abstract class ProductVersionDetailModelBase : ProductVersionModelBase
{
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
}
