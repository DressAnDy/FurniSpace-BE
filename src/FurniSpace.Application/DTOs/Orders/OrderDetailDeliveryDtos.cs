using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Orders;

public sealed class OrderDetailDeliverySummaryDto
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

public sealed class OrderDetailDeliveryBatchItemDto
{
    public Guid OrderItemId { get; init; }
    public int Quantity { get; init; }
    public string? ProductName { get; init; }
}

public sealed class OrderDetailDeliveryBatchDto
{
    public Guid DeliveryId { get; init; }
    public DeliveryStatus? Status { get; init; }
    public Guid? ProjectScheduleId { get; init; }
    public DateTime? ScheduledStart { get; init; }
    public DateTime? ScheduledEnd { get; init; }
    public string? Location { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public IReadOnlyList<OrderDetailDeliveryBatchItemDto> Items { get; init; } = [];
}
