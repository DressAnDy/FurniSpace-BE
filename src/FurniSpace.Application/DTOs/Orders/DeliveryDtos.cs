using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Orders;

public sealed class DeliveryScheduleSummaryDto
{
    public Guid ProjectScheduleId { get; init; }
    public DateTime ScheduledStart { get; init; }
    public DateTime? ScheduledEnd { get; init; }
    public DateTime? CompletedAt { get; init; }
    public ProjectScheduleStatus? Status { get; init; }
    public Guid? AssignedStaffId { get; init; }
    public string? Location { get; init; }
}

public sealed class DeliveryItemDto
{
    public Guid DeliveryItemId { get; init; }
    public Guid DeliveryId { get; init; }
    public Guid OrderItemId { get; init; }
    public int Quantity { get; init; }
    public string? Note { get; init; }
    public string? ProductNameSnapshot { get; init; }
    public string? ItemName { get; init; }
}

public sealed class DeliveryListItemDto
{
    public Guid DeliveryId { get; init; }
    public Guid OrderId { get; init; }
    public Guid? ProjectScheduleId { get; init; }
    public DeliveryScheduleSummaryDto? Schedule { get; init; }
    public DeliveryStatus? Status { get; init; }
    public Guid? CreatedBy { get; init; }
    public Guid? CompletedBy { get; init; }
    public string? Note { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int ItemCount { get; init; }
}

public sealed class DeliveryDetailDto
{
    public Guid DeliveryId { get; init; }
    public Guid OrderId { get; init; }
    public Guid? ProjectScheduleId { get; init; }
    public DeliveryScheduleSummaryDto? Schedule { get; init; }
    public DeliveryStatus? Status { get; init; }
    public Guid? CreatedBy { get; init; }
    public Guid? CompletedBy { get; init; }
    public string? Note { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int ItemCount { get; init; }
    public IReadOnlyList<DeliveryItemDto> Items { get; init; } = [];
}

public sealed class DeliveryListResponseDto
{
    public IReadOnlyList<DeliveryListItemDto> Items { get; init; } = [];
}

public sealed class DeliveryBatchCompletionDto
{
    public Guid DeliveryId { get; init; }
    public Guid OrderId { get; init; }
    public DeliveryStatus? Status { get; init; }
    public int UpdatedItemCount { get; init; }
    public DateTime? CompletedAt { get; init; }
}

public sealed class OrderDeliveryTrackingSummaryDto
{
    public int TotalOrderedQuantity { get; init; }
    public int TotalDeliveredQuantity { get; init; }
    public int RemainingQuantity { get; init; }
    public int DeliveryProgressPercent { get; init; }
    public int CompletedDeliveryCount { get; init; }
    public int InProgressDeliveryCount { get; init; }
    public int UpcomingDeliveryCount { get; init; }
    public DateTime? NextDeliveryAt { get; init; }
}

public sealed class OrderDeliveryTrackingItemDto
{
    public Guid OrderItemId { get; init; }
    public string? ProductName { get; init; }
    public int OrderedQuantity { get; init; }
    public int DeliveredQuantity { get; init; }
    public int RemainingQuantity { get; init; }
    public OrderItemStatus? Status { get; init; }
}

public sealed class OrderDeliveryTrackingTimelineItemDto
{
    public Guid OrderItemId { get; init; }
    public string? ProductName { get; init; }
    public int BatchQuantity { get; init; }
}

public sealed class OrderDeliveryTrackingTimelineEntryDto
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
    public IReadOnlyList<OrderDeliveryTrackingTimelineItemDto> Items { get; init; } = [];
}

public sealed class OrderDeliveryDetailsDto
{
    public Guid OrderId { get; init; }
    public string? DeliveryAddress { get; init; }
    public string? ReceiverName { get; init; }
    public string? ReceiverPhone { get; init; }
    public string? DeliveryNote { get; init; }
}

public sealed class OrderDeliveryTrackingDto
{
    public Guid OrderId { get; init; }
    public OrderStatus? OrderStatus { get; init; }
    public ProjectStatus? ProjectStatus { get; init; }
    public DateTime? CustomerConfirmedDeliveryAt { get; init; }
    public OrderDeliveryDetailsDto DeliveryDetails { get; init; } = new();
    public OrderDeliveryTrackingSummaryDto Summary { get; init; } = new();
    public IReadOnlyList<OrderDeliveryTrackingItemDto> Items { get; init; } = [];
    public IReadOnlyList<OrderDeliveryTrackingTimelineEntryDto> Timeline { get; init; } = [];
}

public sealed class ProjectDeliverySummaryDto
{
    public ProjectStatus? Status { get; init; }
    public int DeliveredQuantity { get; init; }
    public int TotalQuantity { get; init; }
    public int RemainingQuantity { get; init; }
    public int DeliveryProgressPercent { get; init; }
    public DateTime? NextDeliveryAt { get; init; }
}
