using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.ProjectSchedules;

public sealed class ProjectScheduleListQueryReadModel
{
    public ProjectScheduleType? ScheduleType { get; set; }
    public ProjectScheduleStatus? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 20;
}
