using FurniSpace.Application.Common.Payments;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Constants.Payments;

internal static class PaymentServiceConstants
{
    internal const string PaymentNotFoundMessage = "Payment not found.";
    internal const string ProjectNotFoundMessage = "Project not found.";
    internal const string OrderNotFoundMessage = "Order not found.";
    internal const string ForbiddenMessage = "Forbidden.";
    internal const int MaxPaymentCodeAttempts = 5;
    internal const int MaxPayOsOrderCodeAttempts = 5;
    internal const int MaxTransactionCodeAttempts = 5;

    internal static readonly PaymentStatus[] CollectableDepositStatuses =
        ProjectStartFeeRules.CollectablePaymentStatuses;

    internal static readonly PaymentStatus[] VietQrEligibleStatuses =
    [
        PaymentStatus.PENDING,
        PaymentStatus.PROCESSING
    ];
}
