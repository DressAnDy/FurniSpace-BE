using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Payments;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class PaymentRepository : GenericRepository<Payment>, IPaymentRepository
{
    public PaymentRepository(AppDbContext dbContext)
        : base(dbContext)
    {
    }

    public new Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        return DbContext.PaymentSet.FirstOrDefaultAsync(payment => payment.PaymentId == paymentId, cancellationToken);
    }

    public Task<PaymentDetailReadModel?> GetDetailAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        return BuildDetailQuery()
            .Where(payment => payment.PaymentId == paymentId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<PaymentDetailReadModel?> GetDetailByPaymentCodeAsync(string paymentCode, CancellationToken cancellationToken = default)
    {
        return BuildDetailQuery()
            .Where(payment => payment.PaymentCode == paymentCode)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<PaymentStatusByCodeReadModel?> GetStatusByPaymentCodeAsync(string paymentCode, CancellationToken cancellationToken = default)
    {
        return DbContext.PaymentSet
            .Where(payment => payment.PaymentCode == paymentCode)
            .Select(payment => new PaymentStatusByCodeReadModel
            {
                PaymentId = payment.PaymentId,
                PaymentCode = payment.PaymentCode,
                Status = payment.Status,
                Amount = payment.Amount,
                PaidAmount = payment.PaidAmount,
                RemainingAmount = payment.RemainingAmount,
                PaidAt = payment.PaidAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentListItemReadModel>> GetListAsync(
        PaymentQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return await ApplyFilters(DbContext.PaymentSet.AsQueryable(), query)
            .OrderByDescending(payment => payment.CreatedAt)
            .ThenByDescending(payment => payment.PaymentId)
            .Select(payment => new PaymentListItemReadModel
            {
                PaymentId = payment.PaymentId,
                ProjectId = payment.ProjectId,
                OrderId = payment.OrderId,
                PaymentCode = payment.PaymentCode,
                PaymentType = payment.PaymentType,
                Amount = payment.Amount,
                PaidAmount = payment.PaidAmount,
                RemainingAmount = payment.RemainingAmount,
                Currency = payment.Currency,
                Status = payment.Status,
                ExpiredAt = payment.ExpiredAt,
                PaidAt = payment.PaidAt,
                CreatedAt = payment.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentTransactionReadModel>> GetTransactionsByPaymentIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        return await DbContext.PaymentTransactionSet
            .Where(transaction => transaction.PaymentId == paymentId)
            .OrderByDescending(transaction => transaction.CreatedAt)
            .ThenByDescending(transaction => transaction.PaymentTransactionId)
            .Select(transaction => new PaymentTransactionReadModel
            {
                PaymentTransactionId = transaction.PaymentTransactionId,
                PaymentId = transaction.PaymentId,
                TransactionCode = transaction.TransactionCode,
                TransactionType = transaction.TransactionType,
                Amount = transaction.Amount,
                Currency = transaction.Currency,
                PaymentProvider = transaction.PaymentProvider,
                PaymentMethod = transaction.PaymentMethod,
                ProviderTransactionId = transaction.ProviderTransactionId,
                ProviderReferenceCode = transaction.ProviderReferenceCode,
                Status = transaction.Status,
                TransactionTime = transaction.TransactionTime,
                CreatedAt = transaction.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public Task<bool> PaymentCodeExistsAsync(string paymentCode, CancellationToken cancellationToken = default)
    {
        return DbContext.PaymentSet.AnyAsync(payment => payment.PaymentCode == paymentCode, cancellationToken);
    }

    public Task<bool> TransactionCodeExistsAsync(string transactionCode, CancellationToken cancellationToken = default)
    {
        return DbContext.PaymentTransactionSet.AnyAsync(
            transaction => transaction.TransactionCode == transactionCode,
            cancellationToken);
    }

    public Task<bool> ProviderTransactionExistsAsync(
        PaymentProvider provider,
        string providerTransactionId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.PaymentTransactionSet.AnyAsync(
            transaction =>
                transaction.PaymentProvider == provider &&
                transaction.ProviderTransactionId == providerTransactionId,
            cancellationToken);
    }

    public Task<bool> PayOsOrderCodeExistsAsync(string orderCode, CancellationToken cancellationToken = default)
    {
        return DbContext.PaymentTransactionSet.AnyAsync(
            transaction =>
                transaction.PaymentProvider == PaymentProvider.PAYOS &&
                transaction.ProviderReferenceCode == orderCode,
            cancellationToken);
    }

    public Task<PaymentTransaction?> GetTransactionByProviderReferenceAsync(
        PaymentProvider provider,
        string providerReferenceCode,
        CancellationToken cancellationToken = default)
    {
        return DbContext.PaymentTransactionSet.FirstOrDefaultAsync(
            transaction =>
                transaction.PaymentProvider == provider &&
                transaction.ProviderReferenceCode == providerReferenceCode,
            cancellationToken);
    }

    public Task AddPaymentAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        return DbContext.PaymentSet.AddAsync(payment, cancellationToken).AsTask();
    }

    public Task AddTransactionAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
    {
        return DbContext.PaymentTransactionSet.AddAsync(transaction, cancellationToken).AsTask();
    }

    public void UpdatePayment(Payment payment)
    {
        DbContext.PaymentSet.Update(payment);
    }

    public void UpdateTransaction(PaymentTransaction transaction)
    {
        DbContext.PaymentTransactionSet.Update(transaction);
    }

    private IQueryable<PaymentDetailReadModel> BuildDetailQuery()
    {
        return DbContext.PaymentSet
            .Join(
                DbContext.ProjectSet,
                payment => payment.ProjectId,
                project => project.ProjectId,
                (payment, project) => new PaymentDetailReadModel
                {
                    PaymentId = payment.PaymentId,
                    ProjectId = payment.ProjectId,
                    OrderId = payment.OrderId,
                    QuotationId = payment.QuotationId,
                    PaymentCode = payment.PaymentCode,
                    PaidBy = payment.PaidBy,
                    PaymentType = payment.PaymentType,
                    Amount = payment.Amount,
                    PaidAmount = payment.PaidAmount,
                    RemainingAmount = payment.RemainingAmount,
                    Currency = payment.Currency,
                    Status = payment.Status,
                    ExpiredAt = payment.ExpiredAt,
                    PaidAt = payment.PaidAt,
                    CancelledAt = payment.CancelledAt,
                    Note = payment.Note,
                    CreatedAt = payment.CreatedAt,
                    UpdatedAt = payment.UpdatedAt,
                    CustomerId = project.CustomerId,
                    AssignedSalesId = project.AssignedSalesId,
                    AssignedDesignerId = project.AssignedDesignerId
                });
    }

    private static IQueryable<Payment> ApplyFilters(IQueryable<Payment> query, PaymentQueryReadModel filter)
    {
        if (filter.ProjectId.HasValue)
        {
            query = query.Where(payment => payment.ProjectId == filter.ProjectId.Value);
        }

        if (filter.OrderId.HasValue)
        {
            query = query.Where(payment => payment.OrderId == filter.OrderId.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(payment => payment.Status == filter.Status.Value);
        }

        if (filter.PaymentType.HasValue)
        {
            query = query.Where(payment => payment.PaymentType == filter.PaymentType.Value);
        }

        return query;
    }
}
