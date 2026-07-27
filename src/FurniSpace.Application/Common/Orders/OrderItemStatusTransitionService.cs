using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common.Orders;

public enum OrderItemStatusTransitionOwner
{
    ProductionRequestCreation,
    ProductionRequestCompletion,
    CustomerDeliveryConfirmation,
    OrderCancellation
}

public sealed record OrderItemStatusTransitionError(string ErrorCode, string Message);

public static class OrderItemStatusTransitionService
{
    public const string InvalidTransitionCode = "INVALID_ORDER_ITEM_STATUS_TRANSITION";
    public const string OwnerMismatchCode = "ORDER_ITEM_STATUS_OWNER_MISMATCH";

    private static readonly OrderItemStatusTransitionRule[] Rules =
    [
        new(
            OrderItemStatus.PENDING,
            OrderItemStatus.IN_PRODUCTION,
            OrderItemStatusTransitionOwner.ProductionRequestCreation),
        new(
            OrderItemStatus.IN_PRODUCTION,
            OrderItemStatus.READY,
            OrderItemStatusTransitionOwner.ProductionRequestCompletion),
        new(
            OrderItemStatus.IN_PRODUCTION,
            OrderItemStatus.UNAVAILABLE,
            OrderItemStatusTransitionOwner.ProductionRequestCompletion),
        new(
            OrderItemStatus.READY,
            OrderItemStatus.DELIVERED,
            OrderItemStatusTransitionOwner.CustomerDeliveryConfirmation),
        new(
            OrderItemStatus.PENDING,
            OrderItemStatus.CANCELLED,
            OrderItemStatusTransitionOwner.OrderCancellation),
        new(
            OrderItemStatus.IN_PRODUCTION,
            OrderItemStatus.CANCELLED,
            OrderItemStatusTransitionOwner.OrderCancellation)
    ];

    public static OrderItemStatusTransitionError? Validate(
        OrderItemStatus? currentStatus,
        OrderItemStatus targetStatus,
        OrderItemStatusTransitionOwner owner)
    {
        if (!currentStatus.HasValue)
        {
            return InvalidTransition(currentStatus, targetStatus);
        }

        var matchingRules = Rules
            .Where(rule =>
            rule.From == currentStatus.Value &&
            rule.To == targetStatus)
            .ToList();
        if (matchingRules.Count == 0)
        {
            return InvalidTransition(currentStatus, targetStatus);
        }

        var ownerRuleExists = matchingRules.Exists(rule => rule.Owner == owner);
        return ownerRuleExists
            ? null
            : new OrderItemStatusTransitionError(
                OwnerMismatchCode,
                $"Order item transition {currentStatus} -> {targetStatus} is not owned by {owner}.");
    }

    private static OrderItemStatusTransitionError InvalidTransition(
        OrderItemStatus? currentStatus,
        OrderItemStatus targetStatus)
    {
        return new OrderItemStatusTransitionError(
            InvalidTransitionCode,
            $"Order item transition {currentStatus?.ToString() ?? "NULL"} -> {targetStatus} is invalid.");
    }

    private sealed record OrderItemStatusTransitionRule(
        OrderItemStatus From,
        OrderItemStatus To,
        OrderItemStatusTransitionOwner Owner);
}
