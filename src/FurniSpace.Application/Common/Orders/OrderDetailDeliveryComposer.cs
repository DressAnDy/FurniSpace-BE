using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Orders;

namespace FurniSpace.Application.Common.Orders;

internal static class OrderDetailDeliveryComposer
{
    public static OrderDetailDeliverySummaryDto BuildSummary(OrderDeliveryTrackingReadModel? tracking)
    {
        if (tracking is null)
        {
            return EmptySummary();
        }

        return new OrderDetailDeliverySummaryDto
        {
            TotalOrderedQuantity = tracking.TotalOrderedQuantity,
            TotalDeliveredQuantity = tracking.TotalDeliveredQuantity,
            RemainingQuantity = tracking.RemainingQuantity,
            DeliveryProgressPercent = tracking.DeliveryProgressPercent,
            CompletedDeliveryCount = tracking.CompletedDeliveryCount,
            InProgressDeliveryCount = tracking.InProgressDeliveryCount,
            UpcomingDeliveryCount = tracking.UpcomingDeliveryCount,
            NextDeliveryAt = tracking.NextDeliveryAt
        };
    }

    public static OrderDeliveryDetailsDto BuildDeliveryDetails(OrderDetailReadModel order)
    {
        return new OrderDeliveryDetailsDto
        {
            OrderId = order.OrderId,
            DeliveryAddress = order.DeliveryAddress,
            ReceiverName = order.ReceiverName,
            ReceiverPhone = order.ReceiverPhone,
            DeliveryNote = order.DeliveryNote
        };
    }

    public static IReadOnlyList<OrderDetailDeliveryBatchDto> BuildDeliveries(
        IReadOnlyList<DeliveryListItemReadModel> deliveries,
        IReadOnlyList<DeliveryItemReadModel> deliveryItems)
    {
        if (deliveries.Count == 0)
        {
            return [];
        }

        var itemsByDeliveryId = deliveryItems
            .GroupBy(item => item.DeliveryId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<DeliveryItemReadModel>)group.ToList());

        return deliveries
            .Select(delivery => ToBatchDto(delivery, itemsByDeliveryId))
            .ToList();
    }

    public static bool IsAwaitingCustomerConfirmation(OrderStatus? status)
    {
        return status == OrderStatus.AWAITING_CUSTOMER_CONFIRMATION;
    }

    private static OrderDetailDeliveryBatchDto ToBatchDto(
        DeliveryListItemReadModel delivery,
        IReadOnlyDictionary<Guid, IReadOnlyList<DeliveryItemReadModel>> itemsByDeliveryId)
    {
        itemsByDeliveryId.TryGetValue(delivery.DeliveryId, out var items);
        items ??= [];

        return new OrderDetailDeliveryBatchDto
        {
            DeliveryId = delivery.DeliveryId,
            Status = delivery.Status,
            ProjectScheduleId = delivery.ProjectScheduleId,
            ScheduledStart = delivery.Schedule?.ScheduledStart,
            ScheduledEnd = delivery.Schedule?.ScheduledEnd,
            Location = delivery.Schedule?.Location,
            CreatedAt = delivery.CreatedAt,
            CompletedAt = delivery.CompletedAt,
            Items = items
                .Select(item => new OrderDetailDeliveryBatchItemDto
                {
                    OrderItemId = item.OrderItemId,
                    Quantity = item.Quantity,
                    ProductName = item.ItemName ?? item.ProductNameSnapshot
                })
                .ToList()
        };
    }

    private static OrderDetailDeliverySummaryDto EmptySummary()
    {
        return new OrderDetailDeliverySummaryDto();
    }
}
