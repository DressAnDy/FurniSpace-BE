namespace FurniSpace.Application.DTOs.Projects;

public sealed class ProjectsByUserResponseDto
{
    public IReadOnlyList<ProjectByUserItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}
