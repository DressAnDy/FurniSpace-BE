namespace FurniSpace.Application.DTOs.Payments;

public static class PaymentErrorCodes
{
    public const string PaymentNotFound = "PAYMENT_NOT_FOUND";
    public const string InvalidPaymentFilter = "INVALID_PAYMENT_FILTER";
    public const string ProjectNotFound = "PROJECT_NOT_FOUND";
    public const string InvalidPaymentStatus = "INVALID_PAYMENT_STATUS";
    public const string PaymentNotPayable = "PAYMENT_NOT_PAYABLE";
    public const string PaymentExpired = "PAYMENT_EXPIRED";
    public const string SePayDisabled = "SEPAY_DISABLED";
    public const string PayOsDisabled = "PAYOS_DISABLED";
    public const string InvalidPaymentAmount = "INVALID_PAYMENT_AMOUNT";
    public const string PaymentAlreadyPaid = "PAYMENT_ALREADY_PAID";
    public const string PaymentAmountMismatch = "PAYMENT_AMOUNT_MISMATCH";
    public const string PaymentCurrencyMismatch = "PAYMENT_CURRENCY_MISMATCH";
    public const string PaymentSuccessAlreadyExists = "PAYMENT_SUCCESS_ALREADY_EXISTS";
    public const string PaymentTransactionNotFound = "PAYMENT_TRANSACTION_NOT_FOUND";
    public const string PaymentTransactionNotCancellable = "PAYMENT_TRANSACTION_NOT_CANCELLABLE";
    public const string PaymentTransactionAlreadyProcessing = "PAYMENT_TRANSACTION_ALREADY_PROCESSING";
    public const string SuccessTransactionCannotBeCancelled = "SUCCESS_TRANSACTION_CANNOT_BE_CANCELLED";
    public const string UnsupportedPaymentProvider = "UNSUPPORTED_PAYMENT_PROVIDER";
    public const string UnsupportedPaymentMethod = "UNSUPPORTED_PAYMENT_METHOD";
    public const string PayOsCreateLinkFailed = "PAYOS_CREATE_LINK_FAILED";
    public const string PayOsInvalidSignature = "PAYOS_INVALID_SIGNATURE";
    public const string PayOsMissingOrderCode = "PAYOS_MISSING_ORDER_CODE";
    public const string PayOsAmountMismatch = "PAYOS_AMOUNT_MISMATCH";
    public const string WebhookInvalidSignature = "WEBHOOK_INVALID_SIGNATURE";
    public const string ProjectStartFeeAlreadyPaid = "PROJECT_START_FEE_ALREADY_PAID";
    public const string DesignerAlreadyAssigned = "DESIGNER_ALREADY_ASSIGNED";
    public const string ProjectStartFeeRequired = "PROJECT_START_FEE_REQUIRED";
}
