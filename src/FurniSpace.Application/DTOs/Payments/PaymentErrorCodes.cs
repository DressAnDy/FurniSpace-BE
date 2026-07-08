namespace FurniSpace.Application.DTOs.Payments;

public static class PaymentErrorCodes
{
    public const string PaymentNotFound = "PAYMENT_NOT_FOUND";
    public const string ProjectNotFound = "PROJECT_NOT_FOUND";
    public const string InvalidPaymentStatus = "INVALID_PAYMENT_STATUS";
    public const string PaymentExpired = "PAYMENT_EXPIRED";
    public const string SePayDisabled = "SEPAY_DISABLED";
    public const string PayOsDisabled = "PAYOS_DISABLED";
    public const string InvalidPaymentAmount = "INVALID_PAYMENT_AMOUNT";
    public const string PaymentAlreadyPaid = "PAYMENT_ALREADY_PAID";
    public const string PaymentAmountExceedsRemaining = "PAYMENT_AMOUNT_EXCEEDS_REMAINING";
    public const string PaymentTransactionNotFound = "PAYMENT_TRANSACTION_NOT_FOUND";
    public const string PayOsCreateLinkFailed = "PAYOS_CREATE_LINK_FAILED";
    public const string PayOsInvalidSignature = "PAYOS_INVALID_SIGNATURE";
    public const string PayOsMissingOrderCode = "PAYOS_MISSING_ORDER_CODE";
    public const string PayOsAmountMismatch = "PAYOS_AMOUNT_MISMATCH";
    public const string WebhookInvalidSignature = "WEBHOOK_INVALID_SIGNATURE";
}
