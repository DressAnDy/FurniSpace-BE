#nullable enable

using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Financial;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed partial class FinancialReadRepository
{
    private const string CollectionNotCreated = "NOT_CREATED";
    private const string CollectionPending = "PENDING";
    private const string CollectionProcessing = "PROCESSING";
    private const string CollectionExpired = "EXPIRED";
    private const string CollectionFailed = "FAILED";

    private static readonly PaymentType[] OrderScopedPaymentTypes =
    [
        PaymentType.DEPOSIT,
        PaymentType.REMAINING_PAYMENT
    ];

    public async Task<AdminFinancialReceivablesSummaryReadModel> GetReceivablesSummaryAsync(
        AdminFinancialReceivablesQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var items = await LoadEnrichedReceivableItemsAsync(query, utcNow, cancellationToken);
        var activePayments = items.Where(i =>
            i.CollectionState is CollectionPending or CollectionProcessing).ToList();

        return new AdminFinancialReceivablesSummaryReadModel
        {
            OutstandingPaymentAmount = activePayments.Sum(i => i.ActivePaymentAmount ?? 0m),
            OutstandingPaymentCount = activePayments.Count,
            ContractedReceivableAmount = items.Sum(i => i.RemainingAmount ?? 0m),
            OrdersWithReceivableCount = items.Count,
            WithoutPaymentCount = items.Count(i => i.CollectionState == CollectionNotCreated),
            ActiveCollectionCount = activePayments.Count,
            ExpiredPaymentCount = items.Count(i => i.CollectionState == CollectionExpired),
            FailedPaymentCount = items.Count(i => i.CollectionState == CollectionFailed)
        };
    }

    public async Task<int> CountReceivableItemsAsync(
        AdminFinancialReceivablesQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var items = await LoadEnrichedReceivableItemsAsync(query, utcNow, cancellationToken);
        return items.Count;
    }

    public async Task<IReadOnlyList<AdminFinancialReceivableItemReadModel>> GetReceivableItemsAsync(
        AdminFinancialReceivablesQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var items = await LoadEnrichedReceivableItemsAsync(query, utcNow, cancellationToken);
        return SortReceivableItems(items, query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();
    }

    public async Task<AdminFinancialReceivableDetailReadModel?> GetReceivableOrderDetailAsync(
        Guid orderId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var orderRow = await (
            from order in _dbContext.OrderSet.AsNoTracking()
            join project in _dbContext.ProjectSet.AsNoTracking() on order.ProjectId equals project.ProjectId
            join customer in _dbContext.AccountSet.AsNoTracking() on project.CustomerId equals customer.AccountId into customers
            from customer in customers.DefaultIfEmpty()
            where order.OrderId == orderId
            select new
            {
                order.OrderId,
                order.OrderCode,
                order.Status,
                order.ConfirmedAt,
                order.CreatedAt,
                order.FinalTotalAmount,
                order.PaidAmount,
                order.RemainingAmount,
                project.ProjectId,
                project.ProjectCode,
                project.ProjectName,
                project.CustomerId,
                CustomerName = customer != null ? customer.FullName : null
            }).FirstOrDefaultAsync(cancellationToken);

        if (orderRow is null
            || !orderRow.Status.HasValue
            || !ActiveReceivableOrderStatuses.Contains(orderRow.Status.Value)
            || !orderRow.RemainingAmount.HasValue
            || orderRow.RemainingAmount.Value <= 0m)
        {
            return null;
        }

        var payments = await LoadOrderScopedPaymentsAsync(new List<Guid> { orderId }, cancellationToken);
        var paymentIds = payments.Select(p => p.PaymentId).ToList();
        var transactions = await LoadPaymentTransactionsAsync(paymentIds, cancellationToken);
        var enriched = EnrichReceivableItem(
            new ReceivableOrderSeed(
                orderRow.OrderId,
                orderRow.OrderCode ?? string.Empty,
                orderRow.Status,
                orderRow.ConfirmedAt,
                orderRow.CreatedAt,
                orderRow.FinalTotalAmount,
                orderRow.PaidAmount,
                orderRow.RemainingAmount,
                orderRow.ProjectId,
                orderRow.ProjectCode,
                orderRow.ProjectName,
                orderRow.CustomerId,
                orderRow.CustomerName),
            payments,
            transactions,
            utcNow);

        var rounds = BuildPaymentRounds(
            payments,
            transactions,
            remainingAmount: orderRow.RemainingAmount ?? 0m);

        return new AdminFinancialReceivableDetailReadModel
        {
            OrderId = enriched.OrderId,
            OrderCode = enriched.OrderCode,
            OrderStatus = enriched.OrderStatus,
            ConfirmedAt = enriched.ConfirmedAt,
            FinalTotalAmount = enriched.FinalTotalAmount,
            PaidAmount = enriched.PaidAmount,
            RemainingAmount = enriched.RemainingAmount,
            ProjectId = enriched.ProjectId,
            ProjectCode = enriched.ProjectCode,
            ProjectName = enriched.ProjectName,
            CustomerId = enriched.CustomerId,
            CustomerName = enriched.CustomerName,
            CollectionState = enriched.CollectionState,
            ReceivableAgeDays = enriched.ReceivableAgeDays,
            PaymentProgressPercentage = enriched.PaymentProgressPercentage,
            LastPaidAt = enriched.LastPaidAt,
            ActivePaymentId = enriched.ActivePaymentId,
            ActivePaymentCode = payments
                .FirstOrDefault(p => p.PaymentId == enriched.ActivePaymentId)?.PaymentCode,
            ActivePaymentType = enriched.ActivePaymentType,
            ActivePaymentAmount = enriched.ActivePaymentAmount,
            ActivePaymentStatus = enriched.ActivePaymentStatus,
            ActivePaymentExpiredAt = enriched.ActivePaymentExpiredAt,
            PaymentRounds = rounds
        };
    }

    private async Task<List<AdminFinancialReceivableItemReadModel>> LoadEnrichedReceivableItemsAsync(
        AdminFinancialReceivablesQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var seeds = await LoadReceivableOrderSeedsAsync(query, utcNow, cancellationToken);
        if (seeds.Count == 0)
        {
            return [];
        }

        var orderIds = seeds.Select(s => s.OrderId).ToList();
        var payments = await LoadOrderScopedPaymentsAsync(orderIds, cancellationToken);
        var paymentIds = payments.Select(p => p.PaymentId).ToList();
        var transactions = await LoadPaymentTransactionsAsync(paymentIds, cancellationToken);
        var paymentsByOrder = payments
            .Where(p => p.OrderId.HasValue)
            .GroupBy(p => p.OrderId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var items = seeds.Select(seed =>
        {
            paymentsByOrder.TryGetValue(seed.OrderId, out var orderPayments);
            return EnrichReceivableItem(seed, orderPayments ?? [], transactions, utcNow);
        }).ToList();

        if (!string.IsNullOrWhiteSpace(query.CollectionState))
        {
            items = items
                .Where(i => string.Equals(i.CollectionState, query.CollectionState, StringComparison.Ordinal))
                .ToList();
        }

        if (query.PaymentType.HasValue || query.PaymentStatus.HasValue)
        {
            items = items.Where(i =>
            {
                if (!i.ActivePaymentId.HasValue)
                {
                    return false;
                }

                if (query.PaymentType.HasValue && i.ActivePaymentType != query.PaymentType.Value)
                {
                    return false;
                }

                return !query.PaymentStatus.HasValue || i.ActivePaymentStatus == query.PaymentStatus.Value;
            }).ToList();
        }

        return items;
    }

    private async Task<List<ReceivableOrderSeed>> LoadReceivableOrderSeedsAsync(
        AdminFinancialReceivablesQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var orders = BuildReceivableOrderQuery(query, utcNow);
        var rows = await (
            from order in orders
            join project in _dbContext.ProjectSet.AsNoTracking() on order.ProjectId equals project.ProjectId
            join customer in _dbContext.AccountSet.AsNoTracking() on project.CustomerId equals customer.AccountId into customers
            from customer in customers.DefaultIfEmpty()
            select new ReceivableOrderSeed(
                order.OrderId,
                order.OrderCode,
                order.Status,
                order.ConfirmedAt,
                order.CreatedAt,
                order.FinalTotalAmount,
                order.PaidAmount,
                order.RemainingAmount,
                project.ProjectId,
                project.ProjectCode,
                project.ProjectName,
                project.CustomerId,
                customer != null ? customer.FullName : null)).ToListAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(query.Keyword))
        {
            return rows;
        }

        var keyword = query.Keyword.Trim();
        return rows.Where(row =>
            ContainsIgnoreCase(row.OrderCode, keyword) ||
            ContainsIgnoreCase(row.ProjectCode, keyword) ||
            ContainsIgnoreCase(row.ProjectName, keyword) ||
            ContainsIgnoreCase(row.CustomerName, keyword)).ToList();
    }

    private async Task<List<ReceivablePaymentSnapshot>> LoadOrderScopedPaymentsAsync(
        List<Guid> orderIds,
        CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.PaymentSet.AsNoTracking()
            .Where(payment =>
                payment.OrderId.HasValue &&
                orderIds.Contains(payment.OrderId.Value) &&
                payment.PaymentType.HasValue &&
                OrderScopedPaymentTypes.Contains(payment.PaymentType.Value))
            .Select(payment => new ReceivablePaymentSnapshot(
                payment.PaymentId,
                payment.PaymentCode,
                payment.OrderId,
                payment.PaymentType,
                payment.Amount,
                payment.Status,
                payment.CreatedAt,
                payment.PaidAt,
                payment.ExpiredAt))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<ReceivableTransactionSnapshot>> LoadPaymentTransactionsAsync(
        List<Guid> paymentIds,
        CancellationToken cancellationToken)
    {
        if (paymentIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.PaymentTransactionSet.AsNoTracking()
            .Where(transaction => paymentIds.Contains(transaction.PaymentId))
            .Select(transaction => new ReceivableTransactionSnapshot(
                transaction.PaymentId,
                transaction.PaymentProvider,
                transaction.Status,
                transaction.FailureReason,
                transaction.CreatedAt,
                transaction.ConfirmedAt))
            .ToListAsync(cancellationToken);
    }

    private static AdminFinancialReceivableItemReadModel EnrichReceivableItem(
        ReceivableOrderSeed seed,
        IReadOnlyList<ReceivablePaymentSnapshot> payments,
        IReadOnlyList<ReceivableTransactionSnapshot> transactions,
        DateTime utcNow)
    {
        var activePayment = payments
            .Where(payment => IsActiveCollectiblePayment(payment, transactions, utcNow))
            .OrderByDescending(payment => payment.CreatedAt)
            .ThenByDescending(payment => payment.PaymentId)
            .FirstOrDefault();

        var collectionState = ResolveCollectionState(payments, transactions, utcNow, activePayment);
        var lastPaidAt = payments
            .Where(payment => payment.Status == PaymentStatus.PAID && payment.PaidAt.HasValue)
            .Select(payment => payment.PaidAt)
            .Max();

        var failureReason = ResolveLastFailureReason(payments, transactions, activePayment);
        var ageAnchor = seed.ConfirmedAt ?? seed.CreatedAt;
        var paid = seed.PaidAmount ?? 0m;
        var progress = seed.FinalTotalAmount <= 0m
            ? 0m
            : Math.Round(paid * 100m / seed.FinalTotalAmount, 1, MidpointRounding.AwayFromZero);

        return new AdminFinancialReceivableItemReadModel
        {
            ProjectId = seed.ProjectId,
            ProjectCode = seed.ProjectCode,
            ProjectName = seed.ProjectName ?? string.Empty,
            CustomerId = seed.CustomerId,
            CustomerName = seed.CustomerName,
            OrderId = seed.OrderId,
            OrderCode = seed.OrderCode,
            OrderStatus = seed.OrderStatus,
            ConfirmedAt = seed.ConfirmedAt,
            FinalTotalAmount = seed.FinalTotalAmount,
            PaidAmount = seed.PaidAmount,
            RemainingAmount = seed.RemainingAmount,
            PaymentProgressPercentage = progress,
            CollectionState = collectionState,
            ReceivableAgeDays = CalculateReceivableAgeDays(ageAnchor, utcNow),
            LastPaidAt = lastPaidAt,
            ActivePaymentId = activePayment?.PaymentId,
            ActivePaymentType = activePayment?.PaymentType,
            ActivePaymentAmount = activePayment?.Amount,
            ActivePaymentStatus = activePayment?.Status,
            ActivePaymentExpiredAt = activePayment?.ExpiredAt,
            LastPaymentFailureReason = failureReason
        };
    }

    private static List<AdminFinancialReceivablePaymentRoundReadModel> BuildPaymentRounds(
        IReadOnlyList<ReceivablePaymentSnapshot> payments,
        IReadOnlyList<ReceivableTransactionSnapshot> transactions,
        decimal remainingAmount)
    {
        var rounds = payments
            .OrderBy(payment => payment.CreatedAt)
            .ThenBy(payment => payment.PaymentId)
            .Select(payment =>
            {
                var paymentTxns = transactions.Where(t => t.PaymentId == payment.PaymentId).ToList();
                var latest = paymentTxns
                    .OrderByDescending(t => t.CreatedAt)
                    .ThenByDescending(t => t.PaymentId)
                    .FirstOrDefault();
                var failed = paymentTxns.Where(t => t.Status == PaymentTransactionStatus.FAILED).ToList();

                return new AdminFinancialReceivablePaymentRoundReadModel
                {
                    PaymentId = payment.PaymentId,
                    PaymentCode = payment.PaymentCode,
                    PaymentType = payment.PaymentType,
                    Amount = payment.Amount,
                    Status = payment.Status?.ToString() ?? CollectionNotCreated,
                    Provider = latest?.Provider,
                    CreatedAt = payment.CreatedAt,
                    PaidAt = payment.PaidAt,
                    ExpiredAt = payment.ExpiredAt,
                    AttemptCount = paymentTxns.Count,
                    FailedAttemptCount = failed.Count,
                    LastFailureReason = failed
                        .OrderByDescending(t => t.CreatedAt)
                        .Select(t => t.FailureReason)
                        .FirstOrDefault()
                };
            })
            .ToList();

        var hasOpenRemaining = payments.Any(payment =>
            payment.PaymentType == PaymentType.REMAINING_PAYMENT &&
            payment.Status is PaymentStatus.PENDING or PaymentStatus.PROCESSING or PaymentStatus.EXPIRED);

        var hasPaidRemaining = payments.Any(payment =>
            payment.PaymentType == PaymentType.REMAINING_PAYMENT &&
            payment.Status == PaymentStatus.PAID);

        if (remainingAmount > 0m && !hasOpenRemaining && !hasPaidRemaining)
        {
            rounds.Add(new AdminFinancialReceivablePaymentRoundReadModel
            {
                PaymentId = null,
                PaymentType = PaymentType.REMAINING_PAYMENT,
                Amount = remainingAmount,
                Status = CollectionNotCreated
            });
        }

        return rounds;
    }

    private static string ResolveCollectionState(
        IReadOnlyList<ReceivablePaymentSnapshot> payments,
        IReadOnlyList<ReceivableTransactionSnapshot> transactions,
        DateTime utcNow,
        ReceivablePaymentSnapshot? activePayment)
    {
        if (activePayment is not null)
        {
            if (activePayment.Status == PaymentStatus.PROCESSING)
            {
                return CollectionProcessing;
            }

            var activeTxns = transactions.Where(t => t.PaymentId == activePayment.PaymentId).ToList();
            var hasSuccess = activeTxns.Any(t => t.Status == PaymentTransactionStatus.SUCCESS);
            var latestTxn = activeTxns
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefault();
            if (!hasSuccess && latestTxn?.Status == PaymentTransactionStatus.FAILED)
            {
                return CollectionFailed;
            }

            return CollectionPending;
        }

        var latestPayment = payments
            .OrderByDescending(payment => payment.CreatedAt)
            .ThenByDescending(payment => payment.PaymentId)
            .FirstOrDefault();

        if (latestPayment is null)
        {
            return CollectionNotCreated;
        }

        if (IsExpiredPayment(latestPayment, utcNow))
        {
            return CollectionExpired;
        }

        var latestTxns = transactions.Where(t => t.PaymentId == latestPayment.PaymentId).ToList();
        var latestHasSuccess = latestTxns.Any(t => t.Status == PaymentTransactionStatus.SUCCESS);
        var latestAttempt = latestTxns
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefault();

        if (!latestHasSuccess && latestAttempt?.Status == PaymentTransactionStatus.FAILED)
        {
            return CollectionFailed;
        }

        return CollectionNotCreated;
    }

    private static string? ResolveLastFailureReason(
        IReadOnlyList<ReceivablePaymentSnapshot> payments,
        IReadOnlyList<ReceivableTransactionSnapshot> transactions,
        ReceivablePaymentSnapshot? activePayment)
    {
        var focusPaymentId = activePayment?.PaymentId
            ?? payments
                .OrderByDescending(payment => payment.CreatedAt)
                .ThenByDescending(payment => payment.PaymentId)
                .Select(payment => (Guid?)payment.PaymentId)
                .FirstOrDefault();

        if (!focusPaymentId.HasValue)
        {
            return null;
        }

        return transactions
            .Where(t => t.PaymentId == focusPaymentId.Value && t.Status == PaymentTransactionStatus.FAILED)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => t.FailureReason)
            .FirstOrDefault();
    }

    private static bool IsActiveCollectiblePayment(
        ReceivablePaymentSnapshot payment,
        IReadOnlyList<ReceivableTransactionSnapshot> transactions,
        DateTime utcNow)
    {
        if (payment.Status is not (PaymentStatus.PENDING or PaymentStatus.PROCESSING))
        {
            return false;
        }

        if (payment.ExpiredAt.HasValue && payment.ExpiredAt.Value <= utcNow)
        {
            return false;
        }

        return !transactions.Any(t =>
            t.PaymentId == payment.PaymentId &&
            t.Status == PaymentTransactionStatus.SUCCESS);
    }

    private static bool IsExpiredPayment(ReceivablePaymentSnapshot payment, DateTime utcNow)
    {
        if (payment.Status == PaymentStatus.EXPIRED)
        {
            return true;
        }

        return payment.Status is PaymentStatus.PENDING or PaymentStatus.PROCESSING
            && payment.ExpiredAt.HasValue
            && payment.ExpiredAt.Value <= utcNow;
    }

    private IQueryable<Domain.Entities.Order> BuildReceivableOrderQuery(
        AdminFinancialReceivablesQueryReadModel query,
        DateTime utcNow)
    {
        var orders = _dbContext.OrderSet.AsNoTracking()
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

        if (query.MinAgeDays.HasValue)
        {
            var maxConfirmedAt = utcNow.Date.AddDays(-query.MinAgeDays.Value);
            orders = orders.Where(order =>
                order.ConfirmedAt.HasValue && order.ConfirmedAt.Value.Date <= maxConfirmedAt);
        }

        if (query.MaxAgeDays.HasValue)
        {
            var minConfirmedAt = utcNow.Date.AddDays(-query.MaxAgeDays.Value);
            orders = orders.Where(order =>
                order.ConfirmedAt.HasValue && order.ConfirmedAt.Value.Date >= minConfirmedAt);
        }

        return orders;
    }

    private static IEnumerable<AdminFinancialReceivableItemReadModel> SortReceivableItems(
        IEnumerable<AdminFinancialReceivableItemReadModel> items,
        AdminFinancialReceivablesQueryReadModel query)
    {
        var descending = string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        return (query.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "remainingamount" => OrderReceivables(items, i => i.RemainingAmount ?? 0m, descending),
            "receivableagedays" => OrderReceivables(items, i => i.ReceivableAgeDays, descending),
            "ordercode" => OrderReceivables(items, i => i.OrderCode, descending),
            "projectcode" => OrderReceivables(items, i => i.ProjectCode, descending),
            "projectname" => OrderReceivables(items, i => i.ProjectName, descending),
            "orderstatus" => OrderReceivables(items, i => i.OrderStatus, descending),
            "finaltotalamount" => OrderReceivables(items, i => i.FinalTotalAmount, descending),
            _ => OrderReceivables(items, i => i.ConfirmedAt, descending)
        };
    }

    private static IOrderedEnumerable<AdminFinancialReceivableItemReadModel> OrderReceivables<TKey>(
        IEnumerable<AdminFinancialReceivableItemReadModel> items,
        Func<AdminFinancialReceivableItemReadModel, TKey> keySelector,
        bool descending)
    {
        return descending
            ? items.OrderByDescending(keySelector).ThenByDescending(i => i.OrderCode)
            : items.OrderBy(keySelector).ThenBy(i => i.OrderCode);
    }

    private static int CalculateReceivableAgeDays(DateTime? occurredAt, DateTime utcNow)
    {
        if (!occurredAt.HasValue)
        {
            return 0;
        }

        return Math.Max(0, (utcNow.Date - occurredAt.Value.Date).Days);
    }

    private static bool ContainsIgnoreCase(string? source, string keyword) =>
        !string.IsNullOrWhiteSpace(source) &&
        source.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private sealed record ReceivableOrderSeed(
        Guid OrderId,
        string OrderCode,
        OrderStatus? OrderStatus,
        DateTime? ConfirmedAt,
        DateTime? CreatedAt,
        decimal FinalTotalAmount,
        decimal? PaidAmount,
        decimal? RemainingAmount,
        Guid ProjectId,
        string? ProjectCode,
        string? ProjectName,
        Guid CustomerId,
        string? CustomerName);

    private sealed record ReceivablePaymentSnapshot(
        Guid PaymentId,
        string? PaymentCode,
        Guid? OrderId,
        PaymentType? PaymentType,
        decimal Amount,
        PaymentStatus? Status,
        DateTime? CreatedAt,
        DateTime? PaidAt,
        DateTime? ExpiredAt);

    private sealed record ReceivableTransactionSnapshot(
        Guid PaymentId,
        PaymentProvider? Provider,
        PaymentTransactionStatus? Status,
        string? FailureReason,
        DateTime? CreatedAt,
        DateTime? ConfirmedAt);
}
