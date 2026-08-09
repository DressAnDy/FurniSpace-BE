using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Financial;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class FinancialReadRepository : IFinancialReadRepository
{
    private static readonly PaymentStatus[] CollectiblePaymentStatuses =
    [
        PaymentStatus.PENDING,
        PaymentStatus.PROCESSING
    ];

    private static readonly OrderStatus[] ActiveReceivableOrderStatuses =
    [
        OrderStatus.CREATED,
        OrderStatus.DEPOSIT_PENDING,
        OrderStatus.DEPOSIT_PAID,
        OrderStatus.IN_PRODUCTION,
        OrderStatus.READY_FOR_DELIVERY,
        OrderStatus.DELIVERING,
        OrderStatus.DELIVERED,
        OrderStatus.FINAL_PAYMENT_PENDING
    ];

    private readonly AppDbContext _dbContext;

    public FinancialReadRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminFinancialSummaryReadModel> GetAdminSummaryAsync(
        DateTime fromUtc,
        DateTime toUtcExclusive,
        DateTime utcNow,
        string currency,
        IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
        CancellationToken cancellationToken = default)
    {
        var activePaymentQuery = BuildActivePaymentQuery(utcNow, currency);

        return new AdminFinancialSummaryReadModel
        {
            CollectedAmount = await GetCollectedAmountAsync(
                fromUtc,
                toUtcExclusive,
                currency,
                canonicalPaymentTypes,
                cancellationToken),
            OutstandingPaymentAmount = await activePaymentQuery.SumAsync(payment => payment.Amount, cancellationToken),
            ContractedReceivableAmount = await GetContractedReceivableAmountAsync(cancellationToken),
            OrderCommercialValue = await GetOrderCommercialValueAsync(fromUtc, toUtcExclusive, cancellationToken),
            FailedTransactionCount = await GetFailedTransactionCountAsync(fromUtc, toUtcExclusive, currency, cancellationToken),
            ActivePaymentCount = await activePaymentQuery.CountAsync(cancellationToken)
        };
    }

    private Task<decimal> GetCollectedAmountAsync(
        DateTime fromUtc,
        DateTime toUtcExclusive,
        string currency,
        IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
        CancellationToken cancellationToken)
    {
        return _dbContext.PaymentSet
            .Where(payment =>
                payment.Status == PaymentStatus.PAID &&
                payment.PaymentType.HasValue &&
                canonicalPaymentTypes.Contains(payment.PaymentType.Value) &&
                payment.PaidAt.HasValue &&
                payment.PaidAt.Value >= fromUtc &&
                payment.PaidAt.Value < toUtcExclusive &&
                payment.Currency == currency)
            .SumAsync(payment => payment.Amount, cancellationToken);
    }

    private IQueryable<Domain.Entities.Payment> BuildActivePaymentQuery(DateTime utcNow, string currency)
    {
        return _dbContext.PaymentSet
            .Where(payment =>
                payment.Status.HasValue &&
                CollectiblePaymentStatuses.Contains(payment.Status.Value) &&
                (!payment.ExpiredAt.HasValue || payment.ExpiredAt.Value > utcNow) &&
                payment.Currency == currency &&
                !_dbContext.PaymentTransactionSet.Any(transaction =>
                    transaction.PaymentId == payment.PaymentId &&
                    transaction.Status == PaymentTransactionStatus.SUCCESS));
    }

    private Task<decimal> GetContractedReceivableAmountAsync(CancellationToken cancellationToken)
    {
        return _dbContext.OrderSet
            .Where(order =>
                order.Status.HasValue &&
                ActiveReceivableOrderStatuses.Contains(order.Status.Value) &&
                order.RemainingAmount.HasValue &&
                order.RemainingAmount.Value > 0m)
            .SumAsync(order => order.RemainingAmount ?? 0m, cancellationToken);
    }

    private Task<decimal> GetOrderCommercialValueAsync(
        DateTime fromUtc,
        DateTime toUtcExclusive,
        CancellationToken cancellationToken)
    {
        return _dbContext.OrderSet
            .Where(order =>
                order.Status != OrderStatus.CANCELLED &&
                order.ConfirmedAt.HasValue &&
                order.ConfirmedAt.Value >= fromUtc &&
                order.ConfirmedAt.Value < toUtcExclusive)
            .SumAsync(order => order.FinalTotalAmount, cancellationToken);
    }

    private Task<int> GetFailedTransactionCountAsync(
        DateTime fromUtc,
        DateTime toUtcExclusive,
        string currency,
        CancellationToken cancellationToken)
    {
        return _dbContext.PaymentTransactionSet
            .CountAsync(transaction =>
                transaction.Status == PaymentTransactionStatus.FAILED &&
                transaction.CreatedAt.HasValue &&
                transaction.CreatedAt.Value >= fromUtc &&
                transaction.CreatedAt.Value < toUtcExclusive &&
                transaction.Currency == currency,
                cancellationToken);
    }
}
