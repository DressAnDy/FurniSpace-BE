namespace FurniSpace.Infrastructure.DTOs.ProjectSchedules;

public sealed class ProjectScheduleDetailReadModel : ProjectScheduleModelBase
{
    public string ProjectName { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
}
