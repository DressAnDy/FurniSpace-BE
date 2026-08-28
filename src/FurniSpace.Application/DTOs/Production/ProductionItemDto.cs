#nullable enable

namespace FurniSpace.Application.DTOs.Production;

public sealed class ProductionItemDto
{
    public Guid ProductionItemId { get; set; }
    public Guid ProductionRequestId { get; set; }
    public Guid OrderItemId { get; set; }
    public Guid? ProductVersionId { get; set; }
    public string? ProductNameSnapshot { get; set; }
    public string? ProductVersionNameSnapshot { get; set; }
    public int? Quantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? MaterialNote { get; set; }
    public string? ProductionNote { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? OrderItemStatus { get; set; }
}
