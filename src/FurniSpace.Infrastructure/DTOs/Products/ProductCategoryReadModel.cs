namespace FurniSpace.Infrastructure.DTOs.Products;

public sealed class ProductCategoryReadModel
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
}
