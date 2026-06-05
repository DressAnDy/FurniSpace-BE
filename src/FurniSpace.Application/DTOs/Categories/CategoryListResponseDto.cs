namespace FurniSpace.Application.DTOs.Categories;

public sealed class CategoryListResponseDto
{
    public IReadOnlyList<CategoryDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int Limit { get; set; }
    public int Total { get; set; }
}
