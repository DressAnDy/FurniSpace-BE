#nullable enable

using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Production;

public sealed class ProductionRequestQueryDto
{
    public ProductionRequestStatus? Status { get; set; }
    public Guid? AssignedTo { get; set; }
    public string? Priority { get; set; }
}
