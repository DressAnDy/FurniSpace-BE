using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.ProjectSchedules;

public sealed class UpdateProjectScheduleStatusRequestDto
{
    public ProjectScheduleStatus Status { get; set; }
    public string? Note { get; set; }
}
