using System;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Domain.Entities;

public class ProductionItem
{
    public Guid ProductionItemId { get; set; }
    public Guid ProductionRequestId { get; set; }
    public Guid OrderItemId { get; set; }
    public Guid? ProductVersionId { get; set; }
    public string? ProductNameSnapshot { get; set; }
    public string? ProductVersionNameSnapshot { get; set; }
    public int? Quantity { get; set; }
    public DateTime? StartedAt { get; set; }
    public ProductionItemStatus? Status { get; set; }
    public string? MaterialNote { get; set; }
    public string? ProductionNote { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? CompletedAt { get; set; }
}

