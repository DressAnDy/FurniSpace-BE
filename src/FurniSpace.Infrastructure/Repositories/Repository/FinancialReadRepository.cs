using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Financial;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class FinancialReadRepository : IFinancialReadRepository
{
    private const string DefaultCurrency = "VND";
    private const string SeverityHigh = "HIGH";
    private const string SeverityMedium = "MEDIUM";
    private const string TargetOrder = "ORDER";
    private const string TargetPayment = "PAYMENT";
    private const int RepeatedFailureThreshold = 2;
    private const int PendingTooLongDays = 7;
    private const string ExceptionPaymentExpired = "PAYMENT_EXPIRED";
    private const string ExceptionPaymentRepeatedFailure = "PAYMENT_REPEATED_FAILURE";
    private const string ExceptionFinalPaymentNotCreated = "FINAL_PAYMENT_NOT_CREATED";
    private const string ExceptionDeliveredWithReceivable = "DELIVERED_WITH_RECEIVABLE";
    private const string ExceptionPaymentPendingTooLong = "PAYMENT_PENDING_TOO_LONG";
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

    public async Task<AdminFinancialReceivablesSummaryReadModel> GetReceivablesSummaryAsync(
        AdminFinancialReceivablesQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var receivableOrders = BuildReceivableOrderQuery(query, utcNow);
        var activePayments = BuildReceivablePaymentQuery(query, utcNow)
            .Where(payment =>
                payment.OrderId.HasValue &&
                receivableOrders.Any(order => order.OrderId == payment.OrderId.Value));

        return new AdminFinancialReceivablesSummaryReadModel
        {
            OutstandingPaymentAmount = await activePayments.SumAsync(payment => payment.Amount, cancellationToken),
            OutstandingPaymentCount = await activePayments.CountAsync(cancellationToken),
            ContractedReceivableAmount = await receivableOrders.SumAsync(order => order.RemainingAmount ?? 0m, cancellationToken),
            OrdersWithReceivableCount = await receivableOrders.CountAsync(cancellationToken)
        };
    }

    public Task<int> CountReceivableItemsAsync(
        AdminFinancialReceivablesQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        return BuildReceivableOrderQuery(query, utcNow).CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminFinancialReceivableItemReadModel>> GetReceivableItemsAsync(
        AdminFinancialReceivablesQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var activePayments = BuildReceivablePaymentQuery(query, utcNow);
        var rows =
            from order in BuildReceivableOrderQuery(query, utcNow)
            join project in _dbContext.ProjectSet on order.ProjectId equals project.ProjectId
            select new AdminFinancialReceivableItemReadModel
            {
                ProjectId = project.ProjectId,
                ProjectCode = project.ProjectCode,
                ProjectName = project.ProjectName,
                OrderId = order.OrderId,
                OrderCode = order.OrderCode,
                OrderStatus = order.Status,
                ConfirmedAt = order.ConfirmedAt,
                FinalTotalAmount = order.FinalTotalAmount,
                PaidAmount = order.PaidAmount,
                RemainingAmount = order.RemainingAmount,
                ActivePaymentId = activePayments
                    .Where(payment => payment.OrderId == order.OrderId)
                    .OrderByDescending(payment => payment.CreatedAt)
                    .ThenByDescending(payment => payment.PaymentId)
                    .Select(payment => (Guid?)payment.PaymentId)
                    .FirstOrDefault(),
                ActivePaymentType = activePayments
                    .Where(payment => payment.OrderId == order.OrderId)
                    .OrderByDescending(payment => payment.CreatedAt)
                    .ThenByDescending(payment => payment.PaymentId)
                    .Select(payment => payment.PaymentType)
                    .FirstOrDefault(),
                ActivePaymentAmount = activePayments
                    .Where(payment => payment.OrderId == order.OrderId)
                    .OrderByDescending(payment => payment.CreatedAt)
                    .ThenByDescending(payment => payment.PaymentId)
                    .Select(payment => (decimal?)payment.Amount)
                    .FirstOrDefault(),
                ActivePaymentStatus = activePayments
                    .Where(payment => payment.OrderId == order.OrderId)
                    .OrderByDescending(payment => payment.CreatedAt)
                    .ThenByDescending(payment => payment.PaymentId)
                    .Select(payment => payment.Status)
                    .FirstOrDefault()
            };

        return await ApplyReceivableSorting(rows, query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminFinancialPaymentTypeBreakdownReadModel>> GetPaymentBreakdownAsync(
        DateTime fromUtc,
        DateTime toUtcExclusive,
        DateTime utcNow,
        string currency,
        IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
        CancellationToken cancellationToken = default)
    {
        var collectedRows = await GetCollectedAmountsByPaymentTypeAsync(
            fromUtc,
            toUtcExclusive,
            currency,
            canonicalPaymentTypes,
            cancellationToken);
        var paidCounts = await _dbContext.PaymentSet
            .Where(payment =>
                payment.Status == PaymentStatus.PAID &&
                payment.PaymentType.HasValue &&
                canonicalPaymentTypes.Contains(payment.PaymentType.Value) &&
                payment.PaidAt.HasValue &&
                payment.PaidAt.Value >= fromUtc &&
                payment.PaidAt.Value < toUtcExclusive &&
                payment.Currency == currency)
            .GroupBy(payment => payment.PaymentType!.Value)
            .Select(group => new { PaymentType = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var outstandingRows = await BuildActivePaymentQuery(utcNow, currency)
            .Where(payment =>
                payment.PaymentType.HasValue &&
                canonicalPaymentTypes.Contains(payment.PaymentType.Value))
            .GroupBy(payment => payment.PaymentType!.Value)
            .Select(group => new
            {
                PaymentType = group.Key,
                Amount = group.Sum(payment => payment.Amount),
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);
        var expiredRows = await _dbContext.PaymentSet
            .Where(payment =>
                payment.Status == PaymentStatus.EXPIRED &&
                payment.PaymentType.HasValue &&
                canonicalPaymentTypes.Contains(payment.PaymentType.Value) &&
                payment.ExpiredAt.HasValue &&
                payment.ExpiredAt.Value >= fromUtc &&
                payment.ExpiredAt.Value < toUtcExclusive &&
                payment.Currency == currency)
            .GroupBy(payment => payment.PaymentType!.Value)
            .Select(group => new { PaymentType = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return canonicalPaymentTypes
            .Select(paymentType => new AdminFinancialPaymentTypeBreakdownReadModel
            {
                PaymentType = paymentType,
                CollectedAmount = collectedRows.FirstOrDefault(row => row.PaymentType == paymentType)?.Amount ?? 0m,
                PaidCount = paidCounts.FirstOrDefault(row => row.PaymentType == paymentType)?.Count ?? 0,
                OutstandingAmount = outstandingRows.FirstOrDefault(row => row.PaymentType == paymentType)?.Amount ?? 0m,
                OutstandingCount = outstandingRows.FirstOrDefault(row => row.PaymentType == paymentType)?.Count ?? 0,
                ExpiredCount = expiredRows.FirstOrDefault(row => row.PaymentType == paymentType)?.Count ?? 0
            })
            .ToList();
    }

    public async Task<IReadOnlyList<AdminFinancialPaymentTypeAmountReadModel>> GetCollectedAmountsByPaymentTypeAsync(
        DateTime fromUtc,
        DateTime toUtcExclusive,
        string currency,
        IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PaymentSet
            .Where(payment =>
                payment.Status == PaymentStatus.PAID &&
                payment.PaymentType.HasValue &&
                canonicalPaymentTypes.Contains(payment.PaymentType.Value) &&
                payment.PaidAt.HasValue &&
                payment.PaidAt.Value >= fromUtc &&
                payment.PaidAt.Value < toUtcExclusive &&
                payment.Currency == currency)
            .GroupBy(payment => payment.PaymentType!.Value)
            .Select(group => new AdminFinancialPaymentTypeAmountReadModel
            {
                PaymentType = group.Key,
                Amount = group.Sum(payment => payment.Amount)
            })
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountProjectFinancialRowsAsync(
        AdminFinancialProjectsQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        return BuildProjectFinancialBaseQuery(query, utcNow).CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminFinancialProjectRowReadModel>> GetProjectFinancialRowsAsync(
        AdminFinancialProjectsQueryReadModel query,
        DateTime utcNow,
        IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
        CancellationToken cancellationToken = default)
    {
        return await ApplyProjectFinancialSorting(
                BuildProjectFinancialProjection(query, utcNow, canonicalPaymentTypes),
                query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<AdminFinancialProjectRowReadModel?> GetProjectFinancialRowAsync(
        Guid projectId,
        DateTime utcNow,
        IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
        CancellationToken cancellationToken = default)
    {
        var query = new AdminFinancialProjectsQueryReadModel
        {
            ProjectId = projectId,
            Page = 1,
            PageSize = 1
        };

        return BuildProjectFinancialProjection(query, utcNow, canonicalPaymentTypes)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<int> CountOperationalPaymentsAsync(
        AdminFinancialPaymentsQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return BuildOperationalPaymentBaseQuery(query).CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminFinancialPaymentRowReadModel>> GetOperationalPaymentsAsync(
        AdminFinancialPaymentsQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return await ApplyOperationalPaymentSorting(BuildOperationalPaymentProjection(query), query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountFinancialExceptionsAsync(
        AdminFinancialExceptionsQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        return BuildFilteredFinancialExceptions(query, utcNow).CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminFinancialExceptionRowReadModel>> GetFinancialExceptionsAsync(
        AdminFinancialExceptionsQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var rows = await BuildFilteredFinancialExceptions(query, utcNow)
            .OrderByDescending(row => row.OccurredAt)
            .ThenBy(row => row.ExceptionType)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            row.Age = CalculateAge(row.OccurredAt, utcNow);
        }

        return rows;
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

    private IQueryable<Domain.Entities.Payment> BuildOperationalPaymentBaseQuery(
        AdminFinancialPaymentsQueryReadModel query)
    {
        var payments = _dbContext.PaymentSet.AsQueryable();
        if (query.ProjectId.HasValue)
        {
            payments = payments.Where(payment => payment.ProjectId == query.ProjectId.Value);
        }

        if (query.OrderId.HasValue)
        {
            payments = payments.Where(payment => payment.OrderId == query.OrderId.Value);
        }

        if (query.CustomerId.HasValue)
        {
            payments = payments.Where(payment => _dbContext.ProjectSet.Any(project =>
                project.ProjectId == payment.ProjectId &&
                project.CustomerId == query.CustomerId.Value));
        }

        if (query.PaymentType.HasValue)
        {
            payments = payments.Where(payment => payment.PaymentType == query.PaymentType.Value);
        }

        if (query.PaymentStatus.HasValue)
        {
            payments = payments.Where(payment => payment.Status == query.PaymentStatus.Value);
        }

        if (query.Provider.HasValue)
        {
            payments = payments.Where(payment => _dbContext.PaymentTransactionSet.Any(transaction =>
                transaction.PaymentId == payment.PaymentId &&
                transaction.PaymentProvider == query.Provider.Value));
        }

        payments = ApplyPaymentDateFilters(payments, query);
        if (query.HasFailedAttempt.HasValue)
        {
            payments = query.HasFailedAttempt.Value
                ? payments.Where(payment => _dbContext.PaymentTransactionSet.Any(transaction =>
                    transaction.PaymentId == payment.PaymentId &&
                    transaction.Status == PaymentTransactionStatus.FAILED))
                : payments.Where(payment => !_dbContext.PaymentTransactionSet.Any(transaction =>
                    transaction.PaymentId == payment.PaymentId &&
                    transaction.Status == PaymentTransactionStatus.FAILED));
        }

        if (query.MinFailedAttemptCount.HasValue)
        {
            payments = payments.Where(payment =>
                _dbContext.PaymentTransactionSet.Count(transaction =>
                    transaction.PaymentId == payment.PaymentId &&
                    transaction.Status == PaymentTransactionStatus.FAILED) >= query.MinFailedAttemptCount.Value);
        }

        return payments;
    }

    private IQueryable<AdminFinancialPaymentRowReadModel> BuildOperationalPaymentProjection(
        AdminFinancialPaymentsQueryReadModel query)
    {
        return
            from payment in BuildOperationalPaymentBaseQuery(query)
            join project in _dbContext.ProjectSet on payment.ProjectId equals project.ProjectId
            from order in _dbContext.OrderSet
                .Where(order => payment.OrderId.HasValue && order.OrderId == payment.OrderId.Value)
                .DefaultIfEmpty()
            from customer in _dbContext.AccountSet
                .Where(account => account.AccountId == project.CustomerId)
                .DefaultIfEmpty()
            from latestTransaction in _dbContext.PaymentTransactionSet
                .Where(transaction => transaction.PaymentId == payment.PaymentId)
                .OrderByDescending(transaction => transaction.CreatedAt)
                .ThenByDescending(transaction => transaction.PaymentTransactionId)
                .Take(1)
                .DefaultIfEmpty()
            select new AdminFinancialPaymentRowReadModel
            {
                PaymentId = payment.PaymentId,
                PaymentCode = payment.PaymentCode,
                ProjectId = payment.ProjectId,
                ProjectCode = project.ProjectCode,
                OrderId = payment.OrderId,
                OrderCode = order == null ? null : order.OrderCode,
                CustomerId = project.CustomerId,
                CustomerName = customer == null ? null : customer.FullName,
                PaymentType = payment.PaymentType,
                Amount = payment.Amount,
                Currency = payment.Currency,
                Status = payment.Status,
                CreatedAt = payment.CreatedAt,
                ExpiredAt = payment.ExpiredAt,
                PaidAt = payment.PaidAt,
                LastProvider = latestTransaction == null ? null : latestTransaction.PaymentProvider,
                AttemptCount = _dbContext.PaymentTransactionSet.Count(transaction =>
                    transaction.PaymentId == payment.PaymentId),
                FailedAttemptCount = _dbContext.PaymentTransactionSet.Count(transaction =>
                    transaction.PaymentId == payment.PaymentId &&
                    transaction.Status == PaymentTransactionStatus.FAILED),
                LastTransactionStatus = latestTransaction == null ? null : latestTransaction.Status,
                LastFailureReason = _dbContext.PaymentTransactionSet
                    .Where(transaction =>
                        transaction.PaymentId == payment.PaymentId &&
                        transaction.Status == PaymentTransactionStatus.FAILED)
                    .OrderByDescending(transaction => transaction.CreatedAt)
                    .ThenByDescending(transaction => transaction.PaymentTransactionId)
                    .Select(transaction => transaction.FailureReason)
                    .FirstOrDefault(),
                LastAttemptAt = latestTransaction == null ? null : latestTransaction.CreatedAt
            };
    }

    private IQueryable<AdminFinancialExceptionRowReadModel> BuildFilteredFinancialExceptions(
        AdminFinancialExceptionsQueryReadModel query,
        DateTime utcNow)
    {
        var rows = BuildPaymentExpiredExceptions(utcNow)
            .Concat(BuildPaymentRepeatedFailureExceptions(utcNow))
            .Concat(BuildFinalPaymentNotCreatedExceptions(utcNow))
            .Concat(BuildDeliveredWithReceivableExceptions(utcNow))
            .Concat(BuildPaymentPendingTooLongExceptions(utcNow));

        if (!string.IsNullOrWhiteSpace(query.ExceptionType))
        {
            rows = rows.Where(row => row.ExceptionType == query.ExceptionType);
        }

        if (!string.IsNullOrWhiteSpace(query.Severity))
        {
            rows = rows.Where(row => row.Severity == query.Severity);
        }

        if (query.ProjectId.HasValue)
        {
            rows = rows.Where(row => row.ProjectId == query.ProjectId.Value);
        }

        if (query.FromUtc.HasValue)
        {
            rows = rows.Where(row => row.OccurredAt.HasValue && row.OccurredAt.Value >= query.FromUtc.Value);
        }

        if (query.ToUtcExclusive.HasValue)
        {
            rows = rows.Where(row => row.OccurredAt.HasValue && row.OccurredAt.Value < query.ToUtcExclusive.Value);
        }

        if (query.PaymentType.HasValue)
        {
            rows = rows.Where(row =>
                row.PaymentId.HasValue &&
                _dbContext.PaymentSet.Any(payment =>
                    payment.PaymentId == row.PaymentId.Value &&
                    payment.PaymentType == query.PaymentType.Value));
        }

        return rows;
    }

    private IQueryable<AdminFinancialExceptionRowReadModel> BuildPaymentExpiredExceptions(DateTime utcNow)
    {
        return _dbContext.PaymentSet
            .Where(payment => payment.Status == PaymentStatus.EXPIRED)
            .Select(payment => new AdminFinancialExceptionRowReadModel
            {
                ExceptionType = ExceptionPaymentExpired,
                Severity = SeverityMedium,
                ProjectId = payment.ProjectId,
                OrderId = payment.OrderId,
                PaymentId = payment.PaymentId,
                Title = "Payment expired",
                Reason = "Payment checkout validity has expired.",
                Amount = payment.Amount,
                Age = 0,
                OccurredAt = payment.ExpiredAt,
                RecommendedAction = "Review payment and create a new collectible payment if needed.",
                TargetResourceType = TargetPayment,
                TargetResourceId = payment.PaymentId
            });
    }

    private IQueryable<AdminFinancialExceptionRowReadModel> BuildPaymentRepeatedFailureExceptions(DateTime utcNow)
    {
        return _dbContext.PaymentSet
            .Where(payment =>
                payment.Status != PaymentStatus.PAID &&
                _dbContext.PaymentTransactionSet.Count(transaction =>
                    transaction.PaymentId == payment.PaymentId &&
                    transaction.Status == PaymentTransactionStatus.FAILED) >= RepeatedFailureThreshold)
            .Select(payment => new AdminFinancialExceptionRowReadModel
            {
                ExceptionType = ExceptionPaymentRepeatedFailure,
                Severity = SeverityHigh,
                ProjectId = payment.ProjectId,
                OrderId = payment.OrderId,
                PaymentId = payment.PaymentId,
                Title = "Payment has repeated failed attempts",
                Reason = "Payment has two or more failed transaction attempts.",
                Amount = payment.Amount,
                Age = 0,
                OccurredAt = _dbContext.PaymentTransactionSet
                    .Where(transaction =>
                        transaction.PaymentId == payment.PaymentId &&
                        transaction.Status == PaymentTransactionStatus.FAILED)
                    .OrderByDescending(transaction => transaction.CreatedAt)
                    .Select(transaction => transaction.CreatedAt)
                    .FirstOrDefault(),
                RecommendedAction = "Open payment attempts and support the customer with a new checkout if needed.",
                TargetResourceType = TargetPayment,
                TargetResourceId = payment.PaymentId
            });
    }

    private IQueryable<AdminFinancialExceptionRowReadModel> BuildFinalPaymentNotCreatedExceptions(DateTime utcNow)
    {
        var activePayments = BuildActivePaymentQuery(utcNow, DefaultCurrency);
        return _dbContext.OrderSet
            .Where(order =>
                order.Status == OrderStatus.FINAL_PAYMENT_PENDING &&
                order.RemainingAmount.HasValue &&
                order.RemainingAmount.Value > 0m &&
                !activePayments.Any(payment =>
                    payment.OrderId == order.OrderId &&
                    payment.PaymentType == PaymentType.REMAINING_PAYMENT))
            .Select(order => new AdminFinancialExceptionRowReadModel
            {
                ExceptionType = ExceptionFinalPaymentNotCreated,
                Severity = SeverityHigh,
                ProjectId = order.ProjectId,
                OrderId = order.OrderId,
                PaymentId = null,
                Title = "Final payment has not been created",
                Reason = "Order is waiting for final payment but has no active remaining payment.",
                Amount = order.RemainingAmount,
                Age = 0,
                OccurredAt = order.UpdatedAt ?? order.ConfirmedAt ?? order.CreatedAt,
                RecommendedAction = "Create remaining payment for this order.",
                TargetResourceType = TargetOrder,
                TargetResourceId = order.OrderId
            });
    }

    private IQueryable<AdminFinancialExceptionRowReadModel> BuildDeliveredWithReceivableExceptions(DateTime utcNow)
    {
        return _dbContext.OrderSet
            .Where(order =>
                order.Status == OrderStatus.DELIVERED &&
                order.RemainingAmount.HasValue &&
                order.RemainingAmount.Value > 0m)
            .Select(order => new AdminFinancialExceptionRowReadModel
            {
                ExceptionType = ExceptionDeliveredWithReceivable,
                Severity = SeverityMedium,
                ProjectId = order.ProjectId,
                OrderId = order.OrderId,
                PaymentId = null,
                Title = "Delivered order still has receivable",
                Reason = "Order has been delivered but remaining amount is still greater than zero.",
                Amount = order.RemainingAmount,
                Age = 0,
                OccurredAt = order.CustomerConfirmedDeliveryAt ?? order.UpdatedAt ?? order.ConfirmedAt ?? order.CreatedAt,
                RecommendedAction = "Review order receivable and follow up with Sales.",
                TargetResourceType = TargetOrder,
                TargetResourceId = order.OrderId
            });
    }

    private IQueryable<AdminFinancialExceptionRowReadModel> BuildPaymentPendingTooLongExceptions(DateTime utcNow)
    {
        var threshold = utcNow.AddDays(-PendingTooLongDays);
        return BuildActivePaymentQuery(utcNow, DefaultCurrency)
            .Where(payment => payment.CreatedAt.HasValue && payment.CreatedAt.Value <= threshold)
            .Select(payment => new AdminFinancialExceptionRowReadModel
            {
                ExceptionType = ExceptionPaymentPendingTooLong,
                Severity = SeverityMedium,
                ProjectId = payment.ProjectId,
                OrderId = payment.OrderId,
                PaymentId = payment.PaymentId,
                Title = "Payment pending too long",
                Reason = "Payment is still pending or processing after the operational attention threshold.",
                Amount = payment.Amount,
                Age = 0,
                OccurredAt = payment.CreatedAt,
                RecommendedAction = "Check with customer or recreate payment if the checkout is stale.",
                TargetResourceType = TargetPayment,
                TargetResourceId = payment.PaymentId
            });
    }

    private static int CalculateAge(DateTime? occurredAt, DateTime utcNow)
    {
        if (!occurredAt.HasValue || occurredAt.Value >= utcNow)
        {
            return 0;
        }

        return (int)Math.Floor((utcNow - occurredAt.Value).TotalDays);
    }

    private static IQueryable<Domain.Entities.Payment> ApplyPaymentDateFilters(
        IQueryable<Domain.Entities.Payment> payments,
        AdminFinancialPaymentsQueryReadModel query)
    {
        if (query.CreatedFromUtc.HasValue)
        {
            payments = payments.Where(payment => payment.CreatedAt.HasValue && payment.CreatedAt.Value >= query.CreatedFromUtc.Value);
        }

        if (query.CreatedToUtcExclusive.HasValue)
        {
            payments = payments.Where(payment => payment.CreatedAt.HasValue && payment.CreatedAt.Value < query.CreatedToUtcExclusive.Value);
        }

        if (query.PaidFromUtc.HasValue)
        {
            payments = payments.Where(payment => payment.PaidAt.HasValue && payment.PaidAt.Value >= query.PaidFromUtc.Value);
        }

        if (query.PaidToUtcExclusive.HasValue)
        {
            payments = payments.Where(payment => payment.PaidAt.HasValue && payment.PaidAt.Value < query.PaidToUtcExclusive.Value);
        }

        if (query.ExpiredFromUtc.HasValue)
        {
            payments = payments.Where(payment => payment.ExpiredAt.HasValue && payment.ExpiredAt.Value >= query.ExpiredFromUtc.Value);
        }

        if (query.ExpiredToUtcExclusive.HasValue)
        {
            payments = payments.Where(payment => payment.ExpiredAt.HasValue && payment.ExpiredAt.Value < query.ExpiredToUtcExclusive.Value);
        }

        return payments;
    }

    private IQueryable<Domain.Entities.Project> BuildProjectFinancialBaseQuery(
        AdminFinancialProjectsQueryReadModel query,
        DateTime utcNow)
    {
        var projects = _dbContext.ProjectSet.AsQueryable();
        if (query.ProjectId.HasValue)
        {
            projects = projects.Where(project => project.ProjectId == query.ProjectId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            projects = projects.Where(project =>
                (project.ProjectCode != null && project.ProjectCode.Contains(keyword)) ||
                project.ProjectName.Contains(keyword) ||
                _dbContext.AccountSet.Any(account =>
                    account.AccountId == project.CustomerId &&
                    account.FullName.Contains(keyword)));
        }

        if (query.ProjectStatus.HasValue)
        {
            projects = projects.Where(project => project.Status == query.ProjectStatus.Value);
        }

        if (query.CustomerId.HasValue)
        {
            projects = projects.Where(project => project.CustomerId == query.CustomerId.Value);
        }

        if (query.SalesId.HasValue)
        {
            projects = projects.Where(project => project.AssignedSalesId == query.SalesId.Value);
        }

        if (query.FromUtc.HasValue)
        {
            projects = projects.Where(project => project.CreatedAt.HasValue && project.CreatedAt.Value >= query.FromUtc.Value);
        }

        if (query.ToUtcExclusive.HasValue)
        {
            projects = projects.Where(project => project.CreatedAt.HasValue && project.CreatedAt.Value < query.ToUtcExclusive.Value);
        }

        if (query.HasOrder.HasValue)
        {
            projects = query.HasOrder.Value
                ? projects.Where(project => _dbContext.OrderSet.Any(order => order.ProjectId == project.ProjectId))
                : projects.Where(project => !_dbContext.OrderSet.Any(order => order.ProjectId == project.ProjectId));
        }

        if (query.HasReceivable.HasValue)
        {
            projects = query.HasReceivable.Value
                ? projects.Where(project => _dbContext.OrderSet.Any(order =>
                    order.ProjectId == project.ProjectId &&
                    order.Status.HasValue &&
                    ActiveReceivableOrderStatuses.Contains(order.Status.Value) &&
                    order.RemainingAmount.HasValue &&
                    order.RemainingAmount.Value > 0m))
                : projects.Where(project => !_dbContext.OrderSet.Any(order =>
                    order.ProjectId == project.ProjectId &&
                    order.Status.HasValue &&
                    ActiveReceivableOrderStatuses.Contains(order.Status.Value) &&
                    order.RemainingAmount.HasValue &&
                    order.RemainingAmount.Value > 0m));
        }

        if (query.HasOutstandingPayment.HasValue)
        {
            var activePayments = BuildActivePaymentQuery(utcNow, DefaultCurrency);
            projects = query.HasOutstandingPayment.Value
                ? projects.Where(project => activePayments.Any(payment => payment.ProjectId == project.ProjectId))
                : projects.Where(project => !activePayments.Any(payment => payment.ProjectId == project.ProjectId));
        }

        if (query.PaymentType.HasValue || query.PaymentStatus.HasValue)
        {
            var activePayments = BuildProjectPaymentFilterQuery(query, utcNow);
            projects = projects.Where(project => activePayments.Any(payment => payment.ProjectId == project.ProjectId));
        }

        return projects;
    }

    private IQueryable<AdminFinancialProjectRowReadModel> BuildProjectFinancialProjection(
        AdminFinancialProjectsQueryReadModel query,
        DateTime utcNow,
        IReadOnlyCollection<PaymentType> canonicalPaymentTypes)
    {
        var activePayments = BuildActivePaymentQuery(utcNow, DefaultCurrency);
        var projects = BuildProjectFinancialBaseQuery(query, utcNow);
        return
            from project in projects
            from startFee in _dbContext.PaymentSet
                .Where(payment =>
                    payment.ProjectId == project.ProjectId &&
                    payment.PaymentType == PaymentType.PROJECT_START_FEE)
                .OrderByDescending(payment => payment.CreatedAt)
                .ThenByDescending(payment => payment.PaymentId)
                .Take(1)
                .DefaultIfEmpty()
            from latestOrder in _dbContext.OrderSet
                .Where(order => order.ProjectId == project.ProjectId)
                .OrderByDescending(order => order.ConfirmedAt)
                .ThenByDescending(order => order.CreatedAt)
                .ThenByDescending(order => order.OrderId)
                .Take(1)
                .DefaultIfEmpty()
            from activePayment in activePayments
                .Where(payment => payment.ProjectId == project.ProjectId)
                .OrderByDescending(payment => payment.CreatedAt)
                .ThenByDescending(payment => payment.PaymentId)
                .Take(1)
                .DefaultIfEmpty()
            select new AdminFinancialProjectRowReadModel
            {
                ProjectId = project.ProjectId,
                ProjectCode = project.ProjectCode,
                ProjectName = project.ProjectName,
                ProjectStatus = project.Status,
                ProjectCreatedAt = project.CreatedAt,
                CustomerId = project.CustomerId,
                CustomerName = _dbContext.AccountSet
                    .Where(account => account.AccountId == project.CustomerId)
                    .Select(account => account.FullName)
                    .FirstOrDefault(),
                AssignedSalesId = project.AssignedSalesId,
                AssignedSalesName = _dbContext.AccountSet
                    .Where(account => account.AccountId == project.AssignedSalesId)
                    .Select(account => account.FullName)
                    .FirstOrDefault(),
                ProjectStartFeeAmount = startFee == null ? null : startFee.Amount,
                ProjectStartFeeStatus = startFee == null ? null : startFee.Status,
                ProjectStartFeePaidAt = startFee == null ? null : startFee.PaidAt,
                OrderId = latestOrder == null ? null : latestOrder.OrderId,
                OrderCode = latestOrder == null ? null : latestOrder.OrderCode,
                OrderStatus = latestOrder == null ? null : latestOrder.Status,
                OrderOriginalTotal = latestOrder == null ? null : latestOrder.OriginalTotalAmount,
                OrderAdjustmentAmount = latestOrder == null ? null : latestOrder.ItemAdjustmentAmount,
                OrderAdditionalDiscount = latestOrder == null ? null : latestOrder.AdditionalDiscountAmount,
                OrderFinalTotal = latestOrder == null ? null : latestOrder.FinalTotalAmount,
                OrderPaidAmount = latestOrder == null ? null : latestOrder.PaidAmount,
                OrderRemainingAmount = latestOrder == null ? null : latestOrder.RemainingAmount,
                ActivePaymentId = activePayment == null ? null : activePayment.PaymentId,
                ActivePaymentType = activePayment == null ? null : activePayment.PaymentType,
                ActivePaymentAmount = activePayment == null ? null : activePayment.Amount,
                ActivePaymentStatus = activePayment == null ? null : activePayment.Status,
                TotalProjectCashCollected = _dbContext.PaymentSet
                    .Where(payment =>
                        payment.ProjectId == project.ProjectId &&
                        payment.Status == PaymentStatus.PAID &&
                        payment.PaymentType.HasValue &&
                        canonicalPaymentTypes.Contains(payment.PaymentType.Value) &&
                        payment.PaidAt.HasValue &&
                        payment.Currency == DefaultCurrency)
                    .Sum(payment => (decimal?)payment.Amount) ?? 0m,
                LastPaidAt = _dbContext.PaymentSet
                    .Where(payment =>
                        payment.ProjectId == project.ProjectId &&
                        payment.Status == PaymentStatus.PAID &&
                        payment.PaymentType.HasValue &&
                        canonicalPaymentTypes.Contains(payment.PaymentType.Value) &&
                        payment.PaidAt.HasValue &&
                        payment.Currency == DefaultCurrency)
                    .OrderByDescending(payment => payment.PaidAt)
                    .Select(payment => payment.PaidAt)
                    .FirstOrDefault()
            };
    }

    private IQueryable<Domain.Entities.Payment> BuildProjectPaymentFilterQuery(
        AdminFinancialProjectsQueryReadModel query,
        DateTime utcNow)
    {
        var payments = BuildActivePaymentQuery(utcNow, DefaultCurrency);
        if (query.PaymentType.HasValue)
        {
            payments = payments.Where(payment => payment.PaymentType == query.PaymentType.Value);
        }

        if (query.PaymentStatus.HasValue)
        {
            payments = payments.Where(payment => payment.Status == query.PaymentStatus.Value);
        }

        return payments;
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

    private IQueryable<Domain.Entities.Payment> BuildReceivablePaymentQuery(
        AdminFinancialReceivablesQueryReadModel query,
        DateTime utcNow)
    {
        var payments = BuildActivePaymentQuery(utcNow, DefaultCurrency);
        if (query.PaymentType.HasValue)
        {
            payments = payments.Where(payment => payment.PaymentType == query.PaymentType.Value);
        }

        if (query.PaymentStatus.HasValue)
        {
            payments = payments.Where(payment => payment.Status == query.PaymentStatus.Value);
        }

        return payments;
    }

    private IQueryable<Domain.Entities.Order> BuildReceivableOrderQuery(
        AdminFinancialReceivablesQueryReadModel query,
        DateTime utcNow)
    {
        var orders = _dbContext.OrderSet
            .Where(order =>
                order.Status.HasValue &&
                ActiveReceivableOrderStatuses.Contains(order.Status.Value) &&
                order.RemainingAmount.HasValue &&
                order.RemainingAmount.Value > 0m);

        if (query.ProjectId.HasValue)
        {
            orders = orders.Where(order => order.ProjectId == query.ProjectId.Value);
        }

        if (query.CustomerId.HasValue)
        {
            orders = orders.Where(order => order.CustomerId == query.CustomerId.Value);
        }

        if (query.SalesId.HasValue)
        {
            orders = orders.Where(order =>
                order.SalesId == query.SalesId.Value ||
                _dbContext.ProjectSet.Any(project =>
                    project.ProjectId == order.ProjectId &&
                    project.AssignedSalesId == query.SalesId.Value));
        }

        if (query.OrderStatus.HasValue)
        {
            orders = orders.Where(order => order.Status == query.OrderStatus.Value);
        }

        if (query.FromUtc.HasValue)
        {
            orders = orders.Where(order => order.ConfirmedAt.HasValue && order.ConfirmedAt.Value >= query.FromUtc.Value);
        }

        if (query.ToUtcExclusive.HasValue)
        {
            orders = orders.Where(order => order.ConfirmedAt.HasValue && order.ConfirmedAt.Value < query.ToUtcExclusive.Value);
        }

        if (query.PaymentType.HasValue || query.PaymentStatus.HasValue)
        {
            var payments = BuildReceivablePaymentQuery(query, utcNow);
            orders = orders.Where(order => payments.Any(payment => payment.OrderId == order.OrderId));
        }

        return orders;
    }

    private static IQueryable<AdminFinancialReceivableItemReadModel> ApplyReceivableSorting(
        IQueryable<AdminFinancialReceivableItemReadModel> rows,
        AdminFinancialReceivablesQueryReadModel query)
    {
        var descending = string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        return query.SortBy switch
        {
            "projectCode" => descending
                ? rows.OrderByDescending(row => row.ProjectCode).ThenByDescending(row => row.OrderCode)
                : rows.OrderBy(row => row.ProjectCode).ThenBy(row => row.OrderCode),
            "projectName" => descending
                ? rows.OrderByDescending(row => row.ProjectName).ThenByDescending(row => row.OrderCode)
                : rows.OrderBy(row => row.ProjectName).ThenBy(row => row.OrderCode),
            "orderCode" => descending
                ? rows.OrderByDescending(row => row.OrderCode)
                : rows.OrderBy(row => row.OrderCode),
            "orderStatus" => descending
                ? rows.OrderByDescending(row => row.OrderStatus).ThenByDescending(row => row.OrderCode)
                : rows.OrderBy(row => row.OrderStatus).ThenBy(row => row.OrderCode),
            "finalTotalAmount" => descending
                ? rows.OrderByDescending(row => row.FinalTotalAmount).ThenByDescending(row => row.OrderCode)
                : rows.OrderBy(row => row.FinalTotalAmount).ThenBy(row => row.OrderCode),
            "remainingAmount" => descending
                ? rows.OrderByDescending(row => row.RemainingAmount).ThenByDescending(row => row.OrderCode)
                : rows.OrderBy(row => row.RemainingAmount).ThenBy(row => row.OrderCode),
            _ => descending
                ? rows.OrderByDescending(row => row.ConfirmedAt).ThenByDescending(row => row.OrderCode)
                : rows.OrderBy(row => row.ConfirmedAt).ThenBy(row => row.OrderCode)
        };
    }

    private static IQueryable<AdminFinancialPaymentRowReadModel> ApplyOperationalPaymentSorting(
        IQueryable<AdminFinancialPaymentRowReadModel> rows,
        AdminFinancialPaymentsQueryReadModel query)
    {
        var descending = string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        return query.SortBy switch
        {
            "paidAt" => descending
                ? rows.OrderByDescending(row => row.PaidAt).ThenByDescending(row => row.PaymentId)
                : rows.OrderBy(row => row.PaidAt).ThenBy(row => row.PaymentId),
            "expiredAt" => descending
                ? rows.OrderByDescending(row => row.ExpiredAt).ThenByDescending(row => row.PaymentId)
                : rows.OrderBy(row => row.ExpiredAt).ThenBy(row => row.PaymentId),
            "amount" => descending
                ? rows.OrderByDescending(row => row.Amount).ThenByDescending(row => row.PaymentId)
                : rows.OrderBy(row => row.Amount).ThenBy(row => row.PaymentId),
            "paymentCode" => descending
                ? rows.OrderByDescending(row => row.PaymentCode).ThenByDescending(row => row.PaymentId)
                : rows.OrderBy(row => row.PaymentCode).ThenBy(row => row.PaymentId),
            "status" => descending
                ? rows.OrderByDescending(row => row.Status).ThenByDescending(row => row.PaymentId)
                : rows.OrderBy(row => row.Status).ThenBy(row => row.PaymentId),
            _ => descending
                ? rows.OrderByDescending(row => row.CreatedAt).ThenByDescending(row => row.PaymentId)
                : rows.OrderBy(row => row.CreatedAt).ThenBy(row => row.PaymentId)
        };
    }

    private static IQueryable<AdminFinancialProjectRowReadModel> ApplyProjectFinancialSorting(
        IQueryable<AdminFinancialProjectRowReadModel> rows,
        AdminFinancialProjectsQueryReadModel query)
    {
        var descending = string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        return query.SortBy switch
        {
            "projectCode" => descending
                ? rows.OrderByDescending(row => row.ProjectCode).ThenByDescending(row => row.ProjectId)
                : rows.OrderBy(row => row.ProjectCode).ThenBy(row => row.ProjectId),
            "projectName" => descending
                ? rows.OrderByDescending(row => row.ProjectName).ThenByDescending(row => row.ProjectId)
                : rows.OrderBy(row => row.ProjectName).ThenBy(row => row.ProjectId),
            "projectStatus" => descending
                ? rows.OrderByDescending(row => row.ProjectStatus).ThenByDescending(row => row.ProjectId)
                : rows.OrderBy(row => row.ProjectStatus).ThenBy(row => row.ProjectId),
            "orderFinalTotal" => descending
                ? rows.OrderByDescending(row => row.OrderFinalTotal).ThenByDescending(row => row.ProjectId)
                : rows.OrderBy(row => row.OrderFinalTotal).ThenBy(row => row.ProjectId),
            "orderRemainingAmount" => descending
                ? rows.OrderByDescending(row => row.OrderRemainingAmount).ThenByDescending(row => row.ProjectId)
                : rows.OrderBy(row => row.OrderRemainingAmount).ThenBy(row => row.ProjectId),
            "totalProjectCashCollected" => descending
                ? rows.OrderByDescending(row => row.TotalProjectCashCollected).ThenByDescending(row => row.ProjectId)
                : rows.OrderBy(row => row.TotalProjectCashCollected).ThenBy(row => row.ProjectId),
            "lastPaidAt" => descending
                ? rows.OrderByDescending(row => row.LastPaidAt).ThenByDescending(row => row.ProjectId)
                : rows.OrderBy(row => row.LastPaidAt).ThenBy(row => row.ProjectId),
            _ => descending
                ? rows.OrderByDescending(row => row.ProjectCreatedAt).ThenByDescending(row => row.ProjectId)
                : rows.OrderBy(row => row.ProjectCreatedAt).ThenBy(row => row.ProjectId)
        };
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
