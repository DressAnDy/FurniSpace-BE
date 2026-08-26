namespace FurniSpace.Application.Constants.Orders;

internal static class OrderDeliveryConstants
{
    internal const string AllItemsAlreadyDeliveredCancellationNote = "ALL_ITEMS_ALREADY_DELIVERED";
    internal static readonly TimeSpan ScheduleStartTolerance = TimeSpan.FromMinutes(1);
}
