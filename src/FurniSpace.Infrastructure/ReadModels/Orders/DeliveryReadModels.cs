using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Orders;

public sealed class DeliveryScheduleSummaryReadModel
{
    public Guid ProjectScheduleId { get; init; }
    public DateTime ScheduledStart { get; init; }
    public DateTime? ScheduledEnd { get; init; }
    public DateTime? CompletedAt { get; init; }
    public ProjectScheduleStatus? Status { get; init; }
    public Guid? AssignedStaffId { get; init; }
}

public sealed class DeliveryListItemReadModel
{
    public Guid DeliveryId { get; init; }
    public Guid OrderId { get; init; }
    public Guid? ProjectScheduleId { get; init; }
    public DeliveryScheduleSummaryReadModel? Schedule { get; init; }
    public DeliveryStatus? Status { get; init; }
    public Guid? CreatedBy { get; init; }
    public Guid? CompletedBy { get; init; }
    public string? Note { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int ItemCount { get; init; }
}

public sealed class DeliveryDetailReadModel
{
    public Guid DeliveryId { get; init; }
    public Guid OrderId { get; init; }
    public Guid? ProjectScheduleId { get; init; }
    public DeliveryScheduleSummaryReadModel? Schedule { get; init; }
    public DeliveryStatus? Status { get; init; }
    public Guid? CreatedBy { get; init; }
    public Guid? CompletedBy { get; init; }
    public string? Note { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int ItemCount { get; init; }
    public IReadOnlyList<DeliveryItemReadModel> Items { get; init; } = [];
}

public sealed class DeliveryItemReadModel
{
    public Guid DeliveryItemId { get; init; }
    public Guid DeliveryId { get; init; }
    public Guid OrderItemId { get; init; }
    public int Quantity { get; init; }
    public string? Note { get; init; }
    public string? ProductNameSnapshot { get; init; }
    public string? ItemName { get; init; }
}

public sealed class OrderDeliveryTrackingReadModel
{
    public Guid OrderId { get; init; }
    public OrderStatus? OrderStatus { get; init; }
    public ProjectStatus? ProjectStatus { get; init; }
    public DateTime? CustomerConfirmedDeliveryAt { get; init; }
    public string? DeliveryAddress { get; init; }
    public string? ReceiverName { get; init; }
    public string? ReceiverPhone { get; init; }
    public string? DeliveryNote { get; init; }
    public int TotalOrderedQuantity { get; init; }
    public int TotalDeliveredQuantity { get; init; }
    public int RemainingQuantity { get; init; }
    public int DeliveryProgressPercent { get; init; }
    public int CompletedDeliveryCount { get; init; }
    public int UpcomingDeliveryCount { get; init; }
    public DateTime? NextDeliveryAt { get; init; }
    public IReadOnlyList<OrderDeliveryTrackingItemReadModel> Items { get; init; } = [];
    public IReadOnlyList<OrderDeliveryTrackingTimelineEntryReadModel> Timeline { get; init; } = [];
}

public sealed class OrderDeliveryTrackingItemReadModel
{
    public Guid OrderItemId { get; init; }
    public string? ProductName { get; init; }
    public int OrderedQuantity { get; init; }
    public int DeliveredQuantity { get; init; }
    public int RemainingQuantity { get; init; }
    public OrderItemStatus? Status { get; init; }
}

public sealed class OrderDeliveryTrackingTimelineEntryReadModel
{
    public Guid ProjectScheduleId { get; init; }
    public Guid? DeliveryId { get; init; }
    public DateTime ScheduledStart { get; init; }
    public DateTime? ScheduledEnd { get; init; }
    public ProjectScheduleStatus? ScheduleStatus { get; init; }
    public DeliveryStatus? DeliveryStatus { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? CancelReason { get; init; }
    public string? Location { get; init; }
    public Guid? AssignedStaffId { get; init; }
    public string? CustomerNote { get; init; }
    public IReadOnlyList<OrderDeliveryTrackingTimelineItemReadModel> Items { get; init; } = [];
}

public sealed class OrderDeliveryTrackingTimelineItemReadModel
{
    public Guid OrderItemId { get; init; }
    public string? ProductName { get; init; }
    public int BatchQuantity { get; init; }
}

public sealed class ProjectDeliverySummaryReadModel
{
    public ProjectStatus? Status { get; init; }
    public int DeliveredQuantity { get; init; }
    public int TotalQuantity { get; init; }
    public int RemainingQuantity { get; init; }
    public int DeliveryProgressPercent { get; init; }
    public DateTime? NextDeliveryAt { get; init; }
}
