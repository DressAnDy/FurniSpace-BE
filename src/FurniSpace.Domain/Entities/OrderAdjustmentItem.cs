using System;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Domain.Entities;

public class OrderAdjustmentItem
{
    public Guid OrderAdjustmentItemId { get; set; }
    public Guid OrderAdjustmentId { get; set; }
    public Guid? OrderItemId { get; set; }
    public OrderAdjustmentItemType AdjustmentType { get; set; }
    public decimal PreviousItemAmount { get; set; }
    public decimal AdjustmentAmount { get; set; }
    public string Reason { get; set; } = null!;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
