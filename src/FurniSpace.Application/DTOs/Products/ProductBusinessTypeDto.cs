namespace FurniSpace.Application.DTOs.Products;

public sealed class ProductBusinessTypeDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Status { get; set; }
}
