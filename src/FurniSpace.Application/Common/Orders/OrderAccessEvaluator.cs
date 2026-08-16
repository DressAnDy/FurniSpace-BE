using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common.Orders;

internal static class OrderAccessEvaluator
{
    public const string ProductionRole = "PRODUCTION";

    private static readonly OrderStatus[] ProductionVisibleStatuses =
    [
        OrderStatus.DEPOSIT_PAID,
        OrderStatus.IN_PRODUCTION,
        OrderStatus.READY_FOR_DELIVERY,
        OrderStatus.DELIVERING,
        OrderStatus.DELIVERED,
        OrderStatus.FINAL_PAYMENT_PENDING,
        OrderStatus.COMPLETED
    ];

    public static bool CanViewOrder(
        string? role,
        Guid customerId,
        Guid? assignedSalesId,
        Guid? assignedDesignerId,
        Guid currentUserId,
        OrderStatus? orderStatus)
    {
        if (role == ProductionRole)
        {
            return orderStatus.HasValue && ProductionVisibleStatuses.Contains(orderStatus.Value);
        }

        return ProjectAssignmentAccessEvaluator.CanAccessProjectAssignment(
            role,
            customerId,
            assignedSalesId,
            assignedDesignerId,
            currentUserId);
    }

    public static bool CanManageDepositPayment(
        string? role,
        Guid customerId,
        Guid? assignedSalesId,
        Guid currentUserId)
    {
        return role switch
        {
            ProjectAssignmentAccessEvaluator.AdminRole => true,
            ProjectAssignmentAccessEvaluator.CustomerRole => customerId == currentUserId,
            ProjectAssignmentAccessEvaluator.SalesRole => assignedSalesId == currentUserId,
            _ => false
        };
    }
}
