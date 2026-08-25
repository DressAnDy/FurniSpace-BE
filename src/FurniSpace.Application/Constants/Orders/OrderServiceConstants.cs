namespace FurniSpace.Application.Constants.Orders;

internal static class OrderServiceConstants
{
    internal const string ProjectNotFoundMessage = "Project not found.";
    internal const string OrderNotFoundMessage = "Order not found.";
    internal const string ForbiddenMessage = "You do not have permission to update this order.";
    internal const string OrderItemNotFoundMessage = "Order item not found.";
    internal const string LegacyStartDeliveryRestrictedMessage =
        "Legacy start-delivery is restricted to Admin recovery.";
    internal const string LegacyCompleteDeliveryRestrictedMessage =
        "Legacy complete-delivery is restricted to Admin recovery.";
}
