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
    private const string AdminRole = "ADMIN";
    private const string CustomerRole = "CUSTOMER";
    private const string SalesRole = "SALES";
    private const string DesignerRole = "DESIGNER";

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
                PaidAt = payment.PaidAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentListItemReadModel>> GetListAsync(
        PaymentQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return await BuildListQuery(query)
            .OrderByDescending(payment => payment.CreatedAt)
            .ThenByDescending(payment => payment.PaymentId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(PaymentQueryReadModel query, CancellationToken cancellationToken = default)
    {
        return BuildScopedPaymentQuery(query).CountAsync(cancellationToken);
    }

    public async Task<PaymentSummaryReadModel> GetSummaryAsync(
        PaymentQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var scopedQuery = BuildScopedPaymentQuery(query);
        var summary = new PaymentSummaryReadModel
        {
            PendingCount = await scopedQuery.CountAsync(payment => payment.Status == PaymentStatus.PENDING, cancellationToken),
            ProcessingCount = await scopedQuery.CountAsync(payment => payment.Status == PaymentStatus.PROCESSING, cancellationToken),
            PaidCount = await scopedQuery.CountAsync(payment => payment.Status == PaymentStatus.PAID, cancellationToken),
            ExpiredCount = await scopedQuery.CountAsync(payment => payment.Status == PaymentStatus.EXPIRED, cancellationToken),
            CancelledCount = await scopedQuery.CountAsync(payment => payment.Status == PaymentStatus.CANCELLED, cancellationToken),
            RefundedCount = await scopedQuery.CountAsync(payment => payment.Status == PaymentStatus.REFUNDED, cancellationToken)
        };

        var payableCandidates = await scopedQuery
            .Where(payment =>
                (payment.Status == PaymentStatus.PENDING || payment.Status == PaymentStatus.PROCESSING) &&
                (!payment.ExpiredAt.HasValue || payment.ExpiredAt > utcNow))
            .Select(payment => new { payment.PaymentId, payment.Amount })
            .ToListAsync(cancellationToken);

        if (payableCandidates.Count == 0)
        {
            return summary;
        }

        var candidateIds = payableCandidates.Select(item => item.PaymentId).ToList();
        var paidPaymentIds = await DbContext.PaymentTransactionSet
            .Where(transaction =>
                candidateIds.Contains(transaction.PaymentId) &&
                transaction.Status == PaymentTransactionStatus.SUCCESS)
            .Select(transaction => transaction.PaymentId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var paidSet = paidPaymentIds.ToHashSet();
        foreach (var candidate in payableCandidates)
        {
            if (paidSet.Contains(candidate.PaymentId))
            {
                continue;
            }

            summary.PayableCount++;
            summary.PayablePendingAmount += candidate.Amount;
        }

        return summary;
    }

    public async Task<IReadOnlyList<Payment>> GetExpiredPaymentsForSyncAsync(
        PaymentQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        return await BuildScopedPaymentQuery(query)
            .Where(payment =>
                payment.ExpiredAt.HasValue &&
                payment.ExpiredAt <= utcNow &&
                payment.Status != PaymentStatus.PAID &&
                payment.Status != PaymentStatus.CANCELLED &&
                payment.Status != PaymentStatus.REFUNDED &&
                payment.Status != PaymentStatus.EXPIRED)
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
            .Select(transaction => MapTransactionReadModel(transaction))
            .ToListAsync(cancellationToken);
    }

    public Task<PaymentTransaction?> GetTransactionByIdAsync(
        Guid paymentTransactionId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.PaymentTransactionSet.FirstOrDefaultAsync(
            transaction => transaction.PaymentTransactionId == paymentTransactionId,
            cancellationToken);
    }

    public Task<PaymentTransactionReadModel?> GetLatestPendingTransactionAsync(
        Guid paymentId,
        PaymentProvider provider,
        PaymentMethod method,
        CancellationToken cancellationToken = default)
    {
        return DbContext.PaymentTransactionSet
            .Where(transaction =>
                transaction.PaymentId == paymentId &&
                transaction.Status == PaymentTransactionStatus.PENDING &&
                transaction.PaymentProvider == provider &&
                transaction.PaymentMethod == method)
            .OrderByDescending(transaction => transaction.CreatedAt)
            .ThenByDescending(transaction => transaction.PaymentTransactionId)
            .Select(transaction => MapTransactionReadModel(transaction))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<PaymentTransactionReadModel?> GetLatestTransactionAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.PaymentTransactionSet
            .Where(transaction => transaction.PaymentId == paymentId)
            .OrderByDescending(transaction => transaction.CreatedAt)
            .ThenByDescending(transaction => transaction.PaymentTransactionId)
            .Select(transaction => MapTransactionReadModel(transaction))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<Guid>> GetPaymentIdsWithSuccessfulTransactionAsync(
        IReadOnlyCollection<Guid> paymentIds,
        CancellationToken cancellationToken = default)
    {
        if (paymentIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var ids = await DbContext.PaymentTransactionSet
            .Where(transaction =>
                paymentIds.Contains(transaction.PaymentId) &&
                transaction.Status == PaymentTransactionStatus.SUCCESS)
            .Select(transaction => transaction.PaymentId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
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

    public Task<Payment?> GetByOrderAndTypeAsync(
        Guid orderId,
        PaymentType paymentType,
        CancellationToken cancellationToken = default)
    {
        return DbContext.PaymentSet
            .Where(payment => payment.OrderId == orderId && payment.PaymentType == paymentType)
            .OrderByDescending(payment => payment.CreatedAt)
            .ThenByDescending(payment => payment.PaymentId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Payment?> GetByProjectAndTypeAsync(
        Guid projectId,
        PaymentType paymentType,
        CancellationToken cancellationToken = default)
    {
        return DbContext.PaymentSet
            .Where(payment => payment.ProjectId == projectId && payment.PaymentType == paymentType)
            .OrderByDescending(payment => payment.CreatedAt)
            .ThenByDescending(payment => payment.PaymentId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<decimal> SumOrderScopedPaidAmountAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var paymentTypes = new[]
        {
            PaymentType.DEPOSIT,
            PaymentType.REMAINING_PAYMENT,
            PaymentType.FULL_PAYMENT
        };
        var statuses = new[]
        {
            PaymentStatus.PAID
        };

        return DbContext.PaymentSet
            .Where(payment =>
                payment.OrderId == orderId &&
                payment.PaymentType.HasValue &&
                paymentTypes.Contains(payment.PaymentType.Value) &&
                payment.Status.HasValue &&
                statuses.Contains(payment.Status.Value))
            .SumAsync(payment => payment.Amount, cancellationToken);
    }

    public Task<bool> HasSuccessfulTransactionAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.PaymentTransactionSet.AnyAsync(
            transaction =>
                transaction.PaymentId == paymentId &&
                transaction.Status == PaymentTransactionStatus.SUCCESS,
            cancellationToken);
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

    private IQueryable<PaymentListItemReadModel> BuildListQuery(PaymentQueryReadModel query)
    {
        return BuildScopedPaymentQuery(query)
            .Join(
                DbContext.ProjectSet,
                payment => payment.ProjectId,
                project => project.ProjectId,
                (payment, project) => new { payment, project })
            .GroupJoin(
                DbContext.OrderSet,
                joined => joined.payment.OrderId,
                order => order.OrderId,
                (joined, orders) => new { joined.payment, joined.project, orders })
            .SelectMany(
                joined => joined.orders.DefaultIfEmpty(),
                (joined, order) => new PaymentListItemReadModel
                {
                    PaymentId = joined.payment.PaymentId,
                    ProjectId = joined.payment.ProjectId,
                    OrderId = joined.payment.OrderId,
                    PaidBy = joined.payment.PaidBy,
                    PaymentCode = joined.payment.PaymentCode,
                    ProjectCode = joined.project.ProjectCode,
                    ProjectName = joined.project.ProjectName,
                    OrderCode = order != null ? order.OrderCode : null,
                    PaymentType = joined.payment.PaymentType,
                    Amount = joined.payment.Amount,
                    Currency = joined.payment.Currency,
                    Status = joined.payment.Status,
                    ExpiredAt = joined.payment.ExpiredAt,
                    PaidAt = joined.payment.PaidAt,
                    CreatedAt = joined.payment.CreatedAt
                });
    }

    private IQueryable<Payment> BuildScopedPaymentQuery(PaymentQueryReadModel filter)
    {
        var query = ApplyFilters(DbContext.PaymentSet.AsQueryable(), filter);
        return ApplyAccessScope(query, filter);
    }

    private IQueryable<Payment> ApplyAccessScope(IQueryable<Payment> query, PaymentQueryReadModel filter)
    {
        if (string.Equals(filter.AccessRole, AdminRole, StringComparison.Ordinal))
        {
            return query;
        }

        if (string.Equals(filter.AccessRole, CustomerRole, StringComparison.Ordinal))
        {
            return query.Where(payment => payment.PaidBy == filter.AccessUserId);
        }

        if (string.Equals(filter.AccessRole, SalesRole, StringComparison.Ordinal))
        {
            var projectIds = DbContext.ProjectSet
                .Where(project => project.AssignedSalesId == filter.AccessUserId)
                .Select(project => project.ProjectId);
            return query.Where(payment => projectIds.Contains(payment.ProjectId));
        }

        if (string.Equals(filter.AccessRole, DesignerRole, StringComparison.Ordinal))
        {
            var projectIds = DbContext.ProjectSet
                .Where(project => project.AssignedDesignerId == filter.AccessUserId)
                .Select(project => project.ProjectId);
            return query.Where(payment => projectIds.Contains(payment.ProjectId));
        }

        return query.Where(_ => false);
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

    private static PaymentTransactionReadModel MapTransactionReadModel(PaymentTransaction transaction)
    {
        return new PaymentTransactionReadModel
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
            PaymentUrl = transaction.PaymentUrl,
            QrContent = transaction.QrContent,
            FailureReason = transaction.FailureReason,
            TransactionTime = transaction.TransactionTime,
            CreatedAt = transaction.CreatedAt
        };
    }
}
