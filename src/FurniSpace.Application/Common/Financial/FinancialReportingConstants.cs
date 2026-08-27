using System.Collections.Generic;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common.Financial;

public static class FinancialReportingConstants
{
    public const string DefaultCurrency = "VND";
    public const string ReportingTimezone = "Asia/Ho_Chi_Minh";
    public const string CurrencyInvalidMessage = "Financial currency is invalid.";
    internal const string PeriodThisMonth = "THIS_MONTH";
    internal const string PeriodThisYear = "THIS_YEAR";
    internal const string PeriodCustom = "CUSTOM";

    public static IReadOnlyCollection<PaymentType> CanonicalCollectedPaymentTypes { get; } =
    [
        PaymentType.PROJECT_START_FEE,
        PaymentType.DEPOSIT,
        PaymentType.REMAINING_PAYMENT
    ];
}
