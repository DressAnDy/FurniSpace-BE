using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.Projects;
using FurniSpace.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Common.Payments;

internal static class PaymentNotificationSupport
{
    internal const string PaymentReferenceType = "PAYMENT";
    internal const string PaymentCodeParameter = "PaymentCode";
    internal const string PaymentTypeParameter = "PaymentType";
    internal const string AmountParameter = "Amount";
    internal const string CurrencyParameter = "Currency";

    internal static Task TryDispatchCreatedAsync(
        INotificationDispatcher? notifications,
        ILogger? logger,
        Payment payment,
        CancellationToken cancellationToken = default)
    {
        if (payment.PaidBy is null)
        {
            return Task.CompletedTask;
        }

        return TryDispatchAsync(
            notifications,
            logger,
            NotificationType.PaymentCreated,
            payment,
            [payment.PaidBy.Value],
            cancellationToken);
    }

    internal static async Task TryDispatchUpdatedAsync(
        INotificationDispatcher? notifications,
        IProjectStakeholderResolver? stakeholders,
        ILogger? logger,
        Payment payment,
        CancellationToken cancellationToken = default)
    {
        var receiverIds = await ResolvePaymentUpdatedReceiversAsync(stakeholders, payment, cancellationToken);
        if (receiverIds.Count == 0)
        {
            return;
        }

        await TryDispatchAsync(
            notifications,
            logger,
            NotificationType.PaymentPaid,
            payment,
            receiverIds,
            cancellationToken);
    }

    internal static Task TryDispatchAsync(
        INotificationDispatcher? notifications,
        ILogger? logger,
        NotificationType type,
        Payment payment,
        IReadOnlyCollection<Guid> receiverIds,
        CancellationToken cancellationToken = default)
    {
        if (notifications is null || receiverIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        var parameters = new Dictionary<string, string>
        {
            [PaymentCodeParameter] = payment.PaymentCode,
            [PaymentTypeParameter] = payment.PaymentType?.ToString() ?? string.Empty,
            [AmountParameter] = payment.Amount.ToString("0"),
            [CurrencyParameter] = payment.Currency
        };

        var metadata = BuildMetadata(payment);

        try
        {
            return notifications.DispatchAsync(
                type,
                parameters,
                receiverIds,
                new NotificationDispatchRequest(
                    payment.ProjectId,
                    PaymentReferenceType,
                    payment.PaymentId,
                    metadata),
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger?.LogWarning(
                exception,
                "Failed to dispatch payment notification {NotificationType} for payment {PaymentId}",
                type,
                payment.PaymentId);
            return Task.CompletedTask;
        }
    }

    internal static async Task<IReadOnlyList<Guid>> ResolvePaymentUpdatedReceiversAsync(
        IProjectStakeholderResolver? stakeholders,
        Payment payment,
        CancellationToken cancellationToken)
    {
        var receivers = new HashSet<Guid>();
        if (payment.PaidBy.HasValue)
        {
            receivers.Add(payment.PaidBy.Value);
        }

        if (stakeholders is not null)
        {
            var projectStakeholders = await stakeholders.ResolveAsync(payment.ProjectId, cancellationToken);
            if (projectStakeholders?.AssignedSalesId is Guid salesId)
            {
                receivers.Add(salesId);
            }
        }

        return [.. receivers];
    }

    private static Dictionary<string, object?> BuildMetadata(Payment payment)
    {
        return new Dictionary<string, object?>
        {
            ["paymentType"] = payment.PaymentType?.ToString(),
            ["orderId"] = payment.OrderId,
            ["quotationId"] = payment.QuotationId
        };
    }
}
