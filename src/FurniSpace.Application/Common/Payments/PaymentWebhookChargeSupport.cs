using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Application.Interfaces.Projects;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Common.Payments;

internal static class PaymentWebhookChargeSupport
{
    internal static async Task CommitSuccessfulChargeAsync(
        IUnitOfWork unitOfWork,
        IPaymentRepository payments,
        IPaymentBusinessEffectService paymentBusinessEffects,
        Payment payment,
        PaymentTransaction transaction,
        bool isExistingTransaction,
        CancellationToken cancellationToken)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (isExistingTransaction)
            {
                payments.UpdateTransaction(transaction);
            }
            else
            {
                await payments.AddTransactionAsync(transaction, cancellationToken);
            }

            payments.UpdatePayment(payment);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            await paymentBusinessEffects.ApplyAsync(payment, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    internal static async Task PushPaymentUpdatedAsync(
        IPaymentRealtimeService paymentRealtime,
        IProjectStakeholderResolver? stakeholders,
        ILogger? logger,
        Payment payment,
        PaymentTransaction transaction,
        DateTime occurredAt,
        CancellationToken cancellationToken)
    {
        var isPaid = payment.Status == PaymentStatus.PAID;
        var payload = new PaymentUpdatedRealtimeDto
        {
            PaymentId = payment.PaymentId,
            ProjectId = payment.ProjectId,
            PaymentCode = payment.PaymentCode,
            Status = payment.Status,
            Amount = payment.Amount,
            PaidAmount = isPaid ? payment.Amount : 0m,
            RemainingAmount = isPaid ? 0m : payment.Amount,
            PaymentTransactionId = transaction.PaymentTransactionId,
            TransactionAmount = transaction.Amount,
            AppliedAmount = transaction.Amount,
            PaidAt = payment.PaidAt,
            OccurredAt = occurredAt
        };

        var stakeholderUserIds = await PaymentNotificationSupport.ResolvePaymentUpdatedReceiversAsync(
            stakeholders,
            payment,
            cancellationToken);

        try
        {
            await paymentRealtime.SendPaymentUpdatedAsync(
                payload,
                stakeholderUserIds,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger?.LogWarning(
                exception,
                "Failed to push payment.updated realtime event. PaymentId={PaymentId}, PaymentTransactionId={PaymentTransactionId}",
                payment.PaymentId,
                transaction.PaymentTransactionId);
        }
    }
}
