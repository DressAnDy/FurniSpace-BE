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
}
