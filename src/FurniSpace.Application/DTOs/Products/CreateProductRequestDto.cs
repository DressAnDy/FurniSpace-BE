namespace FurniSpace.Application.DTOs.Products;

public sealed class CreateProductRequestDto
{
    public Guid CategoryId { get; set; }
    public string? ProductCode { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ProductType { get; set; }
}
