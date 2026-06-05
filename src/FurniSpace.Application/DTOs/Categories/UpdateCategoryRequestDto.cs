namespace FurniSpace.Application.DTOs.Categories;

public sealed class UpdateCategoryRequestDto
{
    public string CategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
}
