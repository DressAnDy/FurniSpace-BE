namespace FurniSpace.Application.DTOs.Orders;

public static class OrderErrorCodes
{
    public const string ProjectNotFound = "PROJECT_NOT_FOUND";
    public const string OrderNotFound = "ORDER_NOT_FOUND";
    public const string InvalidOrderStatus = "INVALID_ORDER_STATUS";
    public const string DepositAlreadyPaid = "DEPOSIT_ALREADY_PAID";
    public const string RemainingPaymentAlreadyPaid = "REMAINING_PAYMENT_ALREADY_PAID";
    public const string OrderPaymentAlreadyStarted = "ORDER_PAYMENT_ALREADY_STARTED";
    public const string InvalidFinancialAdjustment = "INVALID_FINANCIAL_ADJUSTMENT";
    public const string OrderNotInProduction = "ORDER_NOT_IN_PRODUCTION";
    public const string InvalidAdjustment = "INVALID_ADJUSTMENT";
    public const string InvalidAdjustmentItem = "INVALID_ADJUSTMENT_ITEM";
    public const string InvalidUnavailableItemAmount = "INVALID_UNAVAILABLE_ITEM_AMOUNT";
    public const string ProductionItemNotCancelled = "PRODUCTION_ITEM_NOT_CANCELLED";
    public const string AdjustmentAlreadyConfirmed = "ADJUSTMENT_ALREADY_CONFIRMED";
    public const string AdjustmentItemRequired = "ADJUSTMENT_ITEM_REQUIRED";
    public const string InvalidAdjustmentStatus = "INVALID_ADJUSTMENT_STATUS";
    public const string OrderAdjustmentNotFound = "ORDER_ADJUSTMENT_NOT_FOUND";
    public const string OrderItemNotFound = "ORDER_ITEM_NOT_FOUND";
    public const string DeliveryScheduleNotConfirmed = "DELIVERY_SCHEDULE_NOT_CONFIRMED";
}
