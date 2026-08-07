using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Common.Payments;

internal static class PaymentCustomerNotificationSupport
{
    internal const string PaymentReferenceType = "PAYMENT";
    internal const string PaymentCodeParameter = "PaymentCode";
    internal const string PaymentTypeParameter = "PaymentType";
    internal const string AmountParameter = "Amount";
    internal const string CurrencyParameter = "Currency";

    internal static async Task TryDispatchAsync(
        INotificationDispatcher? notifications,
        ILogger? logger,
        NotificationType type,
        Payment payment,
        IReadOnlyDictionary<string, string>? extraParameters = null,
        CancellationToken cancellationToken = default)
    {
        if (notifications is null || payment.PaidBy is null)
        {
            return;
        }

        var parameters = new Dictionary<string, string>
        {
            [PaymentCodeParameter] = payment.PaymentCode,
            [PaymentTypeParameter] = payment.PaymentType?.ToString() ?? string.Empty,
            [AmountParameter] = payment.Amount.ToString("0"),
            [CurrencyParameter] = payment.Currency
        };

        if (extraParameters is not null)
        {
            foreach (var (key, value) in extraParameters)
            {
                parameters[key] = value;
            }
        }

        try
        {
            await notifications.DispatchAsync(
                type,
                parameters,
                [payment.PaidBy.Value],
                projectId: payment.ProjectId,
                referenceType: PaymentReferenceType,
                referenceId: payment.PaymentId,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger?.LogWarning(
                exception,
                "Failed to dispatch payment notification {NotificationType} for payment {PaymentId}",
                type,
                payment.PaymentId);
        }
    }
}
