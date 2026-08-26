using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.ProjectSchedules;

public sealed class ProjectScheduleListItemReadModel
{
    public Guid ScheduleId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProjectAreaId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
    public Guid? AssignedStaffId { get; set; }
    public Guid? CreatedBy { get; set; }
    public ProjectScheduleType? ScheduleType { get; set; }
    public string? Title { get; set; }
    public DateTime ScheduledStart { get; set; }
    public DateTime? ScheduledEnd { get; set; }
    public string? Location { get; set; }
    public ProjectScheduleStatus? Status { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
