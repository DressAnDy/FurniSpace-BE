using FurniSpace.Domain.Common;

namespace FurniSpace.Application.DTOs.Orders;

public sealed class OrderDetailDto : OrderDetailShape
{
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? CustomerConfirmedDeliveryAt { get; init; }
    public bool AwaitingCustomerConfirmation { get; init; }
    public OrderDeliveryDetailsDto DeliveryDetails { get; init; } = new();
    public OrderDetailDeliverySummaryDto DeliverySummary { get; init; } = new();
    public IReadOnlyList<OrderDetailDeliveryBatchDto> Deliveries { get; init; } = [];
    public IReadOnlyList<OrderItemDto> Items { get; init; } = [];
}
