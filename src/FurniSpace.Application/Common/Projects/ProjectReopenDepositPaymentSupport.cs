using FurniSpace.Application.Common.Payments;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Repositories.IRepository;

namespace FurniSpace.Application.Common.Projects;

internal static class ProjectReopenDepositPaymentSupport
{
    internal static async Task<string?> TryCancelOrExpireActiveDepositPaymentsAsync(
        IPaymentRepository payments,
        Guid orderId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var depositPayments = await payments.GetAllByOrderAndTypeAsync(
            orderId,
            PaymentType.DEPOSIT,
            cancellationToken);

        foreach (var payment in depositPayments)
        {
            var errorCode = await TryCancelOrExpirePaymentAsync(payments, payment, utcNow, cancellationToken);
            if (errorCode is not null)
            {
                return errorCode;
            }
        }

        return null;
    }

    private static async Task<string?> TryCancelOrExpirePaymentAsync(
        IPaymentRepository payments,
        Payment payment,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (payment.Status == PaymentStatus.PAID)
        {
            return ProjectReopenProposalErrorCodes.DepositAlreadyPaid;
        }

        if (payment.Status is PaymentStatus.CANCELLED or PaymentStatus.EXPIRED or PaymentStatus.REFUNDED)
        {
            return null;
        }

        if (await payments.HasSuccessfulTransactionAsync(payment.PaymentId, cancellationToken))
        {
            return ProjectReopenProposalErrorCodes.ActiveDepositCannotBeCancelled;
        }

        if (payment.Status == PaymentStatus.PROCESSING)
        {
            return ProjectReopenProposalErrorCodes.ActiveDepositCannotBeCancelled;
        }

        if (PaymentExpirySynchronizer.TryMarkExpiredIfNeeded(payment, utcNow))
        {
            payments.UpdatePayment(payment);
            return null;
        }

        if (payment.Status is not (PaymentStatus.PENDING or PaymentStatus.PROCESSING))
        {
            return ProjectReopenProposalErrorCodes.ActiveDepositCannotBeCancelled;
        }

        var transactions = await payments.GetTransactionEntitiesByPaymentIdAsync(
            payment.PaymentId,
            cancellationToken);
        foreach (var transaction in transactions)
        {
            if (transaction.Status == PaymentTransactionStatus.SUCCESS)
            {
                return ProjectReopenProposalErrorCodes.ActiveDepositCannotBeCancelled;
            }

            if (transaction.Status == PaymentTransactionStatus.PENDING)
            {
                transaction.Status = PaymentTransactionStatus.CANCELLED;
                payments.UpdateTransaction(transaction);
            }
        }

        payment.Status = PaymentStatus.CANCELLED;
        payment.CancelledAt = utcNow;
        payment.UpdatedAt = utcNow;
        payments.UpdatePayment(payment);
        return null;
    }
}
