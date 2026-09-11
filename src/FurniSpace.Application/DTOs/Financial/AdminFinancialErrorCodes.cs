namespace FurniSpace.Application.DTOs.Financial;

public static class AdminFinancialErrorCodes
{
    public const string DateRangeInvalid = "FINANCIAL_DATE_RANGE_INVALID";
    public const string PeriodInvalid = "FINANCIAL_PERIOD_INVALID";
    public const string CurrencyInvalid = "FINANCIAL_CURRENCY_INVALID";
    public const string FilterInvalid = "FINANCIAL_FILTER_INVALID";
    public const string ReceivableFilterInvalid = "FINANCIAL_RECEIVABLE_FILTER_INVALID";
    public const string GranularityInvalid = "FINANCIAL_GRANULARITY_INVALID";
    public const string ProjectFilterInvalid = "FINANCIAL_PROJECT_FILTER_INVALID";
    /// <summary>Preferred project-not-found code for financial APIs.</summary>
    public const string FinancialProjectNotFound = "FINANCIAL_PROJECT_NOT_FOUND";
    /// <summary>Legacy alias kept for backward compatibility.</summary>
    public const string ProjectNotFound = "PROJECT_NOT_FOUND";
    public const string PaymentFilterInvalid = "FINANCIAL_PAYMENT_FILTER_INVALID";
    public const string PaymentNotFound = "FINANCIAL_PAYMENT_NOT_FOUND";
    public const string ExceptionTypeInvalid = "FINANCIAL_EXCEPTION_TYPE_INVALID";
    public const string MetricInvalid = "FINANCIAL_METRIC_INVALID";
    public const string GroupByInvalid = "FINANCIAL_GROUP_BY_INVALID";
    public const string OrderNotFound = "FINANCIAL_ORDER_NOT_FOUND";
}
