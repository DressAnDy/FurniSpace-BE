#nullable enable

namespace FurniSpace.Application.DTOs.Production;

public sealed class AssignProductionRequestDto
{
    public Guid AssignedTo { get; set; }
    public string? AssignmentNote { get; set; }
}
