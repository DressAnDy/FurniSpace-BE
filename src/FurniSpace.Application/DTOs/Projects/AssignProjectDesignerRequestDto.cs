using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Projects;

public sealed class AssignProjectDesignerRequestDto
{
    public Guid DesignerId { get; set; }
    public ProjectSpaceDataStatus? SpaceDataStatus { get; set; }
    public string? Note { get; set; }
    public DateOnly? ProposalDeadline { get; set; }
}
