using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Projects;

public sealed class ProjectDesignerAssignmentDto
{
    public Guid ProjectId { get; set; }
    public AssignedDesignerDto AssignedDesigner { get; set; } = new();
    public ProjectStatus? Status { get; set; }
    public DateTime? DesignerAssignedAt { get; set; }
    public DateOnly? ProposalDeadline { get; set; }
}
