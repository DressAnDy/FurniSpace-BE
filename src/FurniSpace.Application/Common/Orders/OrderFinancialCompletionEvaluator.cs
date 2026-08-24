using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common.Orders;

internal static class OrderFinancialCompletionEvaluator
{
    internal static bool AreDeliverableItemsDelivered(IReadOnlyList<OrderItem> items)
    {
        return !items
            .Where(IsActiveDeliveryItem)
            .Any(item => item.Status != OrderItemStatus.DELIVERED);
    }

    internal static bool IsDeliveryConfirmedForFinancialCompletion(Order order, IReadOnlyList<OrderItem> items)
    {
        return order.CustomerConfirmedDeliveryAt.HasValue &&
            AreDeliverableItemsDelivered(items);
    }

    internal static bool CanAutoCompleteAfterRemainingPayment(
        Order order,
        IReadOnlyList<OrderItem> items,
        decimal remainingAmount)
    {
        if (order.Status == OrderStatus.COMPLETED)
        {
            return false;
        }

        if (order.Status != OrderStatus.FINAL_PAYMENT_PENDING)
        {
            return false;
        }

        if (remainingAmount > 0m)
        {
            return false;
        }

        return IsDeliveryConfirmedForFinancialCompletion(order, items);
    }

    private static bool IsProductLineItem(OrderItem item)
    {
        return item.ProductVersionId.HasValue &&
            (item.Quantity ?? 0) > 0 &&
            item.Status is not (OrderItemStatus.UNAVAILABLE or OrderItemStatus.CANCELLED);
    }

    private static bool IsActiveDeliveryItem(OrderItem item)
    {
        return IsProductLineItem(item) &&
            item.Status is OrderItemStatus.READY
                or OrderItemStatus.PARTIALLY_DELIVERED
                or OrderItemStatus.PHYSICALLY_DELIVERED
                or OrderItemStatus.DELIVERED;
    }
}
