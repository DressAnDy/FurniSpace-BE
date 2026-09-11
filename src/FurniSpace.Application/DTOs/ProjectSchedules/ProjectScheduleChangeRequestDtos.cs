using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.ProjectSchedules;

public sealed class RequestProjectScheduleChangeDto
{
    public string? Note { get; set; }
}

public sealed class ProjectScheduleChangeRequestDto
{
    public Guid ScheduleId { get; set; }
    public ProjectScheduleStatus? Status { get; set; }
    public string? CustomerNote { get; set; }
}
