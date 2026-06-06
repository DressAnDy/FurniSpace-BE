namespace FurniSpace.Application.DTOs.Products;

public sealed class ProductCategorySummaryDto
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
}
