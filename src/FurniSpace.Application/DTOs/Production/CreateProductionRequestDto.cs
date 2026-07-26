#nullable enable

namespace FurniSpace.Application.DTOs.Production;

public sealed class CreateProductionRequestDto
{
    public Guid AssignedTo { get; set; }
    public string? Priority { get; set; }
    public DateOnly? EstimatedStartDate { get; set; }
    public DateOnly? EstimatedCompletionDate { get; set; }
    public string? Note { get; set; }
}
