namespace FurniSpace.Application.DTOs.Categories;

public sealed class CreateCategoryRequestDto
{
    public string CategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
}
