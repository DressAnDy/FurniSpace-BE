using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.ProjectSchedules;

public sealed class ProjectScheduleListQueryDto
{
    public ProjectScheduleType? ScheduleType { get; set; }
    public ProjectScheduleStatus? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 20;
}
