using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Projects;

public sealed class ProjectStatusUpdateDto
{
    public Guid ProjectId { get; set; }
    public ProjectStatus? Status { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
