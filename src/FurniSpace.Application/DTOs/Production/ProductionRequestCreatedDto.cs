#nullable enable

namespace FurniSpace.Application.DTOs.Production;

public sealed class ProductionRequestCreatedDto
{
    public Guid ProductionRequestId { get; set; }
    public string? ProductionCode { get; set; }
    public Guid ProjectId { get; set; }
    public Guid OrderId { get; set; }
    public Guid? AssignedTo { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ProductionItemCount { get; set; }
}
