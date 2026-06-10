using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Projects;

public sealed class ProjectSalesAssignmentDto
{
    public Guid ProjectId { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public ProjectStatus? Status { get; set; }
    public DateTime? SalesAssignedAt { get; set; }
}
