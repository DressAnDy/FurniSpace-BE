namespace FurniSpace.Application.DTOs.Orders;

public static class OrderErrorCodes
{
    public const string ProjectNotFound = "PROJECT_NOT_FOUND";
    public const string OrderNotFound = "ORDER_NOT_FOUND";
    public const string InvalidOrderStatus = "INVALID_ORDER_STATUS";
    public const string DepositAlreadyPaid = "DEPOSIT_ALREADY_PAID";
    public const string RemainingPaymentAlreadyPaid = "REMAINING_PAYMENT_ALREADY_PAID";
    public const string OrderPaymentAlreadyStarted = "ORDER_PAYMENT_ALREADY_STARTED";
    public const string OrderItemNotFound = "ORDER_ITEM_NOT_FOUND";
    public const string DeliveryScheduleNotConfirmed = "DELIVERY_SCHEDULE_NOT_CONFIRMED";
    public const string ItemNotDeliverable = "ITEM_NOT_DELIVERABLE";
    public const string OrderItemNotReady = "ORDER_ITEM_NOT_READY";
    public const string OrderNotDelivering = "ORDER_NOT_DELIVERING";
    public const string OrderNotDelivered = "ORDER_NOT_DELIVERED";
    public const string DeliveryNotConfirmed = "DELIVERY_NOT_CONFIRMED";
    public const string DeliveryNotCompleted = "DELIVERY_NOT_COMPLETED";
    public const string DeliverableItemsNotReady = "DELIVERABLE_ITEMS_NOT_READY";
    public const string DeliverableItemsNotDelivered = "DELIVERABLE_ITEMS_NOT_DELIVERED";
    public const string ProductionNotCompleted = "PRODUCTION_NOT_COMPLETED";
    public const string DeliveryAlreadyCompleted = "DELIVERY_ALREADY_COMPLETED";
    public const string OrderAlreadyDelivered = "ORDER_ALREADY_DELIVERED";
    public const string NegativeRemainingAmount = "NEGATIVE_REMAINING_AMOUNT";
    public const string OrderNotReadyForRemainingPayment = "ORDER_NOT_READY_FOR_REMAINING_PAYMENT";
    public const string RemainingPaymentNotRequired = "REMAINING_PAYMENT_NOT_REQUIRED";
    public const string RemainingPaymentNotPaid = "REMAINING_PAYMENT_NOT_PAID";
    public const string OrderNotReadyToComplete = "ORDER_NOT_READY_TO_COMPLETE";
    public const string LegacyAutoCompletePathDetected = "LEGACY_AUTO_COMPLETE_PATH_DETECTED";
}
