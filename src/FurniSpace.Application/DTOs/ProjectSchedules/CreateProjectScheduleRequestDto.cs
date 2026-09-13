using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.ProjectSchedules;

public sealed class CreateProjectScheduleRequestDto
{
    public ProjectScheduleType ScheduleType { get; set; } = ProjectScheduleType.MEASUREMENT;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public Guid? AssignedStaffId { get; set; }
    public DateTime ScheduledStart { get; set; }
    public DateTime? ScheduledEnd { get; set; }
    public string? Location { get; set; }
    public string? CustomerNote { get; set; }
    public string? InternalNote { get; set; }
}
