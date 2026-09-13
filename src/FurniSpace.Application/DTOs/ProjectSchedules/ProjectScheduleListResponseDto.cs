namespace FurniSpace.Application.DTOs.ProjectSchedules;

public sealed class ProjectScheduleListResponseDto
{
    public IReadOnlyList<ProjectScheduleDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int Limit { get; set; }
}
