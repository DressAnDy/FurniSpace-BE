using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Projects;

public sealed class ProjectRejectionDto
{
    public Guid ProjectId { get; set; }
    public ProjectStatus? Status { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? RejectedAt { get; set; }
}
