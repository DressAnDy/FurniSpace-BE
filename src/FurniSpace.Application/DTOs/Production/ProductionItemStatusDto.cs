#nullable enable

namespace FurniSpace.Application.DTOs.Production;

public sealed class ProductionItemStatusDto
{
    public Guid ProductionItemId { get; set; }
    public Guid ProductionRequestId { get; set; }
    public Guid OrderItemId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ProductionNote { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
