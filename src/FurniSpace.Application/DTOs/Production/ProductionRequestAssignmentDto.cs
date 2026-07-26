#nullable enable

namespace FurniSpace.Application.DTOs.Production;

public sealed class ProductionRequestAssignmentDto
{
    public Guid ProductionRequestId { get; set; }
    public Guid? PreviousAssignedTo { get; set; }
    public Guid? AssignedTo { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
}
