using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Projects;

public sealed class UpdateProjectStatusRequestDto
{
    public ProjectStatus? Status { get; set; }
    public string? Note { get; set; }
}
