namespace FurniSpace.Application.DTOs.Products;

public sealed class UpdateProductRequestDto
{
    public Guid CategoryId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
}
