using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.ProjectSchedules;

public abstract class ProjectScheduleModelBase
{
    public Guid ScheduleId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProjectAreaId { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? AssignedStaffId { get; set; }
    public ProjectScheduleType? ScheduleType { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateTime ScheduledStart { get; set; }
    public DateTime? ScheduledEnd { get; set; }
    public string? Location { get; set; }
    public ProjectScheduleStatus? Status { get; set; }
    public string? CustomerNote { get; set; }
    public string? InternalNote { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public bool? CanMoveToProposalDrafting { get; set; }
}
