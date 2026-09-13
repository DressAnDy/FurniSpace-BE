namespace FurniSpace.Application.DTOs.Projects;

public sealed class ProjectListResponseDto
{
    public IReadOnlyList<ProjectListItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int Limit { get; set; }
    public int Total { get; set; }
}
