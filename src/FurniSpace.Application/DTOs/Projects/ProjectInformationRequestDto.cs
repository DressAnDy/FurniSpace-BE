using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Projects;

public sealed class ProjectInformationRequestDto
{
    public Guid ProjectId { get; set; }
    public ProjectStatus? Status { get; set; }
    public DateTime RequestedAt { get; set; }
}
