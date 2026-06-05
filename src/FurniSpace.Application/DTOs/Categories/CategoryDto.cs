namespace FurniSpace.Application.DTOs.Categories;

public sealed class CategoryDto
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Status { get; set; }
}
