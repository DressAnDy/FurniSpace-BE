using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Categories;

public sealed class CategoryDto
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProductStatus? Status { get; set; }
}
