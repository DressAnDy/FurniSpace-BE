using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Financial;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed partial class FinancialReadRepository : IFinancialReadRepository
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
        var payments = ApplyOperationalPaymentScopeFilters(_dbContext.PaymentSet.AsQueryable(), query);
        payments = ApplyOperationalPaymentStateFilters(payments, query);
        payments = ApplyPaymentDateFilters(payments, query);
        return ApplyOperationalPaymentAttemptFilters(payments, query);
    }

    private IQueryable<Domain.Entities.Payment> ApplyOperationalPaymentScopeFilters(
        IQueryable<Domain.Entities.Payment> payments,
        AdminFinancialPaymentsQueryReadModel query)
    {
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

        return payments;
    }

    private static IQueryable<Domain.Entities.Payment> ApplyOperationalPaymentStateFilters(
        IQueryable<Domain.Entities.Payment> payments,
        AdminFinancialPaymentsQueryReadModel query)
    {
        if (query.PaymentType.HasValue)
        {
            payments = payments.Where(payment => payment.PaymentType == query.PaymentType.Value);
        }

        if (query.PaymentStatus.HasValue)
        {
            payments = payments.Where(payment => payment.Status == query.PaymentStatus.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Currency))
        {
            payments = payments.Where(payment => payment.Currency == query.Currency);
        }

        return payments;
    }

    private IQueryable<Domain.Entities.Payment> ApplyOperationalPaymentAttemptFilters(
        IQueryable<Domain.Entities.Payment> payments,
        AdminFinancialPaymentsQueryReadModel query)
    {
        if (query.Provider.HasValue)
        {
            payments = payments.Where(payment => _dbContext.PaymentTransactionSet.Any(transaction =>
                transaction.PaymentId == payment.PaymentId &&
                transaction.PaymentProvider == query.Provider.Value));
        }

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
        var rows = BuildPaymentExpiredExceptions()
            .Concat(BuildPaymentRepeatedFailureExceptions())
            .Concat(BuildFinalPaymentNotCreatedExceptions(utcNow))
            .Concat(BuildDeliveredWithReceivableExceptions())
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

    private IQueryable<AdminFinancialExceptionRowReadModel> BuildPaymentExpiredExceptions()
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

    private IQueryable<AdminFinancialExceptionRowReadModel> BuildPaymentRepeatedFailureExceptions()
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

    private IQueryable<AdminFinancialExceptionRowReadModel> BuildDeliveredWithReceivableExceptions()
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
        var projects = ApplyProjectFinancialIdentityFilters(_dbContext.ProjectSet.AsQueryable(), query);
        projects = ApplyProjectFinancialDateFilters(projects, query);
        projects = ApplyProjectFinancialOrderFilters(projects, query);
        return ApplyProjectFinancialPaymentFilters(projects, query, utcNow);
    }

    private IQueryable<Domain.Entities.Project> ApplyProjectFinancialIdentityFilters(
        IQueryable<Domain.Entities.Project> projects,
        AdminFinancialProjectsQueryReadModel query)
    {
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

        return projects;
    }

    private static IQueryable<Domain.Entities.Project> ApplyProjectFinancialDateFilters(
        IQueryable<Domain.Entities.Project> projects,
        AdminFinancialProjectsQueryReadModel query)
    {
        if (query.FromUtc.HasValue)
        {
            projects = projects.Where(project => project.CreatedAt.HasValue && project.CreatedAt.Value >= query.FromUtc.Value);
        }

        if (query.ToUtcExclusive.HasValue)
        {
            projects = projects.Where(project => project.CreatedAt.HasValue && project.CreatedAt.Value < query.ToUtcExclusive.Value);
        }

        return projects;
    }

    private IQueryable<Domain.Entities.Project> ApplyProjectFinancialOrderFilters(
        IQueryable<Domain.Entities.Project> projects,
        AdminFinancialProjectsQueryReadModel query)
    {
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

        return projects;
    }

    private IQueryable<Domain.Entities.Project> ApplyProjectFinancialPaymentFilters(
        IQueryable<Domain.Entities.Project> projects,
        AdminFinancialProjectsQueryReadModel query,
        DateTime utcNow)
    {
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
                ProjectStartFeeAmount = (decimal?)startFee.Amount,
                ProjectStartFeeStatus = startFee.Status,
                ProjectStartFeePaidAt = startFee.PaidAt,
                OrderId = (Guid?)latestOrder.OrderId,
                OrderCode = latestOrder.OrderCode,
                OrderStatus = latestOrder.Status,
                OrderFinalTotal = (decimal?)latestOrder.FinalTotalAmount,
                OrderPaidAmount = latestOrder.PaidAmount,
                OrderRemainingAmount = latestOrder.RemainingAmount,
                ActivePaymentId = (Guid?)activePayment.PaymentId,
                ActivePaymentType = activePayment.PaymentType,
                ActivePaymentAmount = (decimal?)activePayment.Amount,
                ActivePaymentStatus = activePayment.Status,
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
                    .FirstOrDefault(),
                CollectedInPeriod = query.FromUtc.HasValue && query.ToUtcExclusive.HasValue
                    ? _dbContext.PaymentSet
                        .Where(payment =>
                            payment.ProjectId == project.ProjectId &&
                            payment.Status == PaymentStatus.PAID &&
                            payment.PaymentType.HasValue &&
                            canonicalPaymentTypes.Contains(payment.PaymentType.Value) &&
                            payment.PaidAt.HasValue &&
                            payment.PaidAt.Value >= query.FromUtc.Value &&
                            payment.PaidAt.Value < query.ToUtcExclusive.Value &&
                            payment.Currency == DefaultCurrency)
                        .Sum(payment => (decimal?)payment.Amount) ?? 0m
                    : 0m,
                LastPaidInPeriod = query.FromUtc.HasValue && query.ToUtcExclusive.HasValue
                    ? _dbContext.PaymentSet
                        .Where(payment =>
                            payment.ProjectId == project.ProjectId &&
                            payment.Status == PaymentStatus.PAID &&
                            payment.PaymentType.HasValue &&
                            canonicalPaymentTypes.Contains(payment.PaymentType.Value) &&
                            payment.PaidAt.HasValue &&
                            payment.PaidAt.Value >= query.FromUtc.Value &&
                            payment.PaidAt.Value < query.ToUtcExclusive.Value &&
                            payment.Currency == DefaultCurrency)
                        .OrderByDescending(payment => payment.PaidAt)
                        .Select(payment => payment.PaidAt)
                        .FirstOrDefault()
                    : null
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
        return descending
            ? ApplyProjectFinancialDescendingSorting(rows, query.SortBy)
            : ApplyProjectFinancialAscendingSorting(rows, query.SortBy);
    }

    private static IQueryable<AdminFinancialProjectRowReadModel> ApplyProjectFinancialAscendingSorting(
        IQueryable<AdminFinancialProjectRowReadModel> rows,
        string? sortBy)
    {
        return sortBy switch
        {
            "projectCode" => rows.OrderBy(row => row.ProjectCode).ThenBy(row => row.ProjectId),
            "projectName" => rows.OrderBy(row => row.ProjectName).ThenBy(row => row.ProjectId),
            "projectStatus" => rows.OrderBy(row => row.ProjectStatus).ThenBy(row => row.ProjectId),
            "orderFinalTotal" => rows.OrderBy(row => row.OrderFinalTotal).ThenBy(row => row.ProjectId),
            "orderRemainingAmount" => rows.OrderBy(row => row.OrderRemainingAmount).ThenBy(row => row.ProjectId),
            "totalProjectCashCollected" => rows.OrderBy(row => row.TotalProjectCashCollected).ThenBy(row => row.ProjectId),
            "lastPaidAt" => rows.OrderBy(row => row.LastPaidAt).ThenBy(row => row.ProjectId),
            "collectedInPeriod" => rows.OrderBy(row => row.CollectedInPeriod).ThenBy(row => row.ProjectId),
            _ => rows.OrderBy(row => row.ProjectCreatedAt).ThenBy(row => row.ProjectId)
        };
    }

    private static IQueryable<AdminFinancialProjectRowReadModel> ApplyProjectFinancialDescendingSorting(
        IQueryable<AdminFinancialProjectRowReadModel> rows,
        string? sortBy)
    {
        return sortBy switch
        {
            "projectCode" => rows.OrderByDescending(row => row.ProjectCode).ThenByDescending(row => row.ProjectId),
            "projectName" => rows.OrderByDescending(row => row.ProjectName).ThenByDescending(row => row.ProjectId),
            "projectStatus" => rows.OrderByDescending(row => row.ProjectStatus).ThenByDescending(row => row.ProjectId),
            "orderFinalTotal" => rows.OrderByDescending(row => row.OrderFinalTotal).ThenByDescending(row => row.ProjectId),
            "orderRemainingAmount" => rows.OrderByDescending(row => row.OrderRemainingAmount).ThenByDescending(row => row.ProjectId),
            "totalProjectCashCollected" => rows.OrderByDescending(row => row.TotalProjectCashCollected).ThenByDescending(row => row.ProjectId),
            "lastPaidAt" => rows.OrderByDescending(row => row.LastPaidAt).ThenByDescending(row => row.ProjectId),
            "collectedInPeriod" => rows.OrderByDescending(row => row.CollectedInPeriod).ThenByDescending(row => row.ProjectId),
            _ => rows.OrderByDescending(row => row.ProjectCreatedAt).ThenByDescending(row => row.ProjectId)
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
