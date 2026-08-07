namespace FurniSpace.Application.Constants.Orders;

internal static class OrderServiceConstants
{
    internal const string ProjectNotFoundMessage = "Project not found.";
    internal const string OrderNotFoundMessage = "Order not found.";
    internal const string ForbiddenMessage = "You do not have permission to update this order financial adjustment.";
    internal const string InvalidOrderStatusMessage = "Order is not pending deposit payment.";
    internal const string OrderAdjustmentNotFoundMessage = "Order adjustment not found.";
    internal const string OrderItemNotFoundMessage = "Order item not found.";
    internal const string OrderNotInProductionMessage = "Order must be in production.";
    internal const string PaymentAlreadyStartedMessage =
        "Order deposit payment has already started and cannot be adjusted.";
    internal const string FinancialAdjustmentUpdatedMessage = "Order financial adjustment updated successfully.";
    internal const string OrderAdjustmentCreatedMessage = "Order adjustment created successfully.";
    internal const string OrderAdjustmentItemUpdatedMessage = "Order adjustment item updated successfully.";
    internal const string OrderAdjustmentItemDeletedMessage = "Order adjustment item deleted successfully.";
}
