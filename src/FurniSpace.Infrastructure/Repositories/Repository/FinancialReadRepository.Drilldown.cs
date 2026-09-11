#nullable enable

using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Financial;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed partial class FinancialReadRepository
{
    private const string DimensionPaymentType = "PAYMENT_TYPE";
    private const string DimensionProject = "PROJECT";
    private const string DimensionProvider = "PROVIDER";
    private const string DimensionStatus = "STATUS";
    private const string DimensionOrderStatus = "ORDER_STATUS";
    private const string DimensionAging = "AGING";
    private const string DimensionFailureReason = "FAILURE_REASON";
    private const string ResourcePayment = "PAYMENT";
    private const string ResourceOrder = "ORDER";
    private const string ResourceTransaction = "TRANSACTION";
    private const string ResourceProject = "PROJECT";
    private const string GroupByProject = "PROJECT";
    private const string Aging0To3 = "0_3";
    private const string Aging4To7 = "4_7";
    private const string Aging8To14 = "8_14";
    private const string AgingOver14 = "OVER_14";
    private const string UnknownKey = "UNKNOWN";

    public async Task<AdminFinancialSummaryDrilldownReadModel> GetSummaryDrilldownAsync(
        AdminFinancialSummaryDrilldownQueryReadModel query,
        DateTime fromUtc,
        DateTime toUtcExclusive,
        DateTime utcNow,
        string currency,
        IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
        CancellationToken cancellationToken = default)
    {
        return query.Metric switch
        {
            "COLLECTED" => await BuildCollectedDrilldownAsync(
                query, fromUtc, toUtcExclusive, utcNow, currency, canonicalPaymentTypes, cancellationToken),
            "OUTSTANDING" => await BuildActivePaymentDrilldownAsync(
                query, utcNow, currency, isOutstandingMetric: true, cancellationToken),
            "ACTIVE_PAYMENTS" => await BuildActivePaymentDrilldownAsync(
                query, utcNow, currency, isOutstandingMetric: false, cancellationToken),
            "CONTRACTED_RECEIVABLE" => await BuildContractedReceivableDrilldownAsync(
                query, utcNow, cancellationToken),
            "ORDER_VALUE" => await BuildOrderValueDrilldownAsync(
                query, fromUtc, toUtcExclusive, utcNow, cancellationToken),
            "FAILED_TRANSACTIONS" => await BuildFailedTransactionsDrilldownAsync(
                query, fromUtc, toUtcExclusive, utcNow, currency, cancellationToken),
            _ => new AdminFinancialSummaryDrilldownReadModel
            {
                Metric = query.Metric,
                Page = query.Page,
                PageSize = query.PageSize
            }
        };
    }

    private async Task<AdminFinancialSummaryDrilldownReadModel> BuildCollectedDrilldownAsync(
        AdminFinancialSummaryDrilldownQueryReadModel query,
        DateTime fromUtc,
        DateTime toUtcExclusive,
        DateTime utcNow,
        string currency,
        IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
        CancellationToken cancellationToken)
    {
        var groupByProject = string.Equals(query.GroupBy, GroupByProject, StringComparison.Ordinal);
        var collectedTypes = groupByProject
            ? canonicalPaymentTypes.Append(PaymentType.FULL_PAYMENT).Distinct().ToArray()
            : canonicalPaymentTypes;

        var baseQuery = ApplyPaymentDrilldownFilters(
            _dbContext.PaymentSet.AsNoTracking()
                .Where(payment =>
                    payment.Status == PaymentStatus.PAID &&
                    payment.PaymentType.HasValue &&
                    collectedTypes.Contains(payment.PaymentType.Value) &&
                    payment.PaidAt.HasValue &&
                    payment.PaidAt.Value >= fromUtc &&
                    payment.PaidAt.Value < toUtcExclusive &&
                    payment.Currency == currency),
            query);

        var rows = await ProjectPaymentDrilldownRowsAsync(baseQuery, cancellationToken);
        var providerByPayment = await LoadSuccessProvidersByPaymentAsync(
            rows.Select(r => r.PaymentId).ToList(),
            cancellationToken);

        var paymentItems = rows.Select(r =>
        {
            providerByPayment.TryGetValue(r.PaymentId, out var provider);
            return ToPaymentItem(r, provider, occurredAt: r.PaidAt, utcNow);
        }).ToList();

        paymentItems = FilterItemsByProvider(paymentItems, query);

        if (groupByProject)
        {
            var projectItems = AggregateCollectedByProject(paymentItems, utcNow);
            projectItems = await EnrichCollectedProjectItemsAsync(projectItems, cancellationToken);
            var kpiTotalAmount = projectItems.Sum(i =>
                (i.ProjectStartFeeAmount ?? 0m) +
                (i.DepositAmount ?? 0m) +
                (i.RemainingPaymentAmount ?? 0m));

            return PageDrilldown(
                "COLLECTED",
                projectItems,
                [
                    BuildPaymentTypeBreakdownFromProjectTotals(projectItems),
                    BuildProjectBreakdown(projectItems),
                    BuildProviderBreakdown(paymentItems)
                ],
                query,
                totalAmountOverride: kpiTotalAmount);
        }

        return PageDrilldown(
            "COLLECTED",
            paymentItems,
            [
                BuildPaymentTypeBreakdown(paymentItems),
                BuildProjectBreakdown(paymentItems),
                BuildProviderBreakdown(paymentItems)
            ],
            query);
    }

    private static List<AdminFinancialDrilldownItemReadModel> AggregateCollectedByProject(
        IReadOnlyList<AdminFinancialDrilldownItemReadModel> paymentItems,
        DateTime utcNow)
    {
        return paymentItems
            .Where(i => i.ProjectId.HasValue)
            .GroupBy(i => i.ProjectId!.Value)
            .Select(g =>
            {
                var first = g.First();
                var startFee = g.Where(i => i.PaymentType == nameof(PaymentType.PROJECT_START_FEE)).Sum(i => i.Amount);
                var deposit = g.Where(i => i.PaymentType == nameof(PaymentType.DEPOSIT)).Sum(i => i.Amount);
                var remaining = g.Where(i => i.PaymentType == nameof(PaymentType.REMAINING_PAYMENT)).Sum(i => i.Amount);
                var full = g.Where(i => i.PaymentType == nameof(PaymentType.FULL_PAYMENT)).Sum(i => i.Amount);
                var totalCollected = startFee + deposit + remaining + full;
                var lastPaidAt = g.Max(i => i.OccurredAt);

                return new AdminFinancialDrilldownItemReadModel
                {
                    ResourceType = ResourceProject,
                    ProjectId = first.ProjectId,
                    ProjectCode = first.ProjectCode,
                    ProjectName = first.ProjectName,
                    Amount = totalCollected,
                    OccurredAt = lastPaidAt,
                    AgeDays = CalculateAgeDays(lastPaidAt, utcNow),
                    ProjectStartFeeAmount = startFee,
                    DepositAmount = deposit,
                    RemainingPaymentAmount = remaining,
                    FullPaymentAmount = full,
                    TotalCollectedAmount = totalCollected,
                    PaymentCount = g.Count(),
                    LastPaidAt = lastPaidAt
                };
            })
            .ToList();
    }

    private async Task<List<AdminFinancialDrilldownItemReadModel>> EnrichCollectedProjectItemsAsync(
        List<AdminFinancialDrilldownItemReadModel> projectItems,
        CancellationToken cancellationToken)
    {
        var projectIds = projectItems
            .Where(i => i.ProjectId.HasValue)
            .Select(i => i.ProjectId!.Value)
            .Distinct()
            .ToList();
        if (projectIds.Count == 0)
        {
            return projectItems;
        }

        var projectCustomers = await (
            from project in _dbContext.ProjectSet.AsNoTracking()
            where projectIds.Contains(project.ProjectId)
            join customer in _dbContext.AccountSet.AsNoTracking()
                on project.CustomerId equals customer.AccountId into customers
            from customer in customers.DefaultIfEmpty()
            select new
            {
                project.ProjectId,
                project.CustomerId,
                CustomerName = customer != null ? customer.FullName : null
            }).ToListAsync(cancellationToken);

        var customerByProject = projectCustomers.ToDictionary(x => x.ProjectId);

        var latestOrders = await _dbContext.OrderSet.AsNoTracking()
            .Where(order => projectIds.Contains(order.ProjectId))
            .Select(order => new
            {
                order.OrderId,
                order.OrderCode,
                order.ProjectId,
                order.FinalTotalAmount,
                order.PaidAmount,
                order.RemainingAmount,
                order.ConfirmedAt,
                order.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var orderByProject = latestOrders
            .GroupBy(o => o.ProjectId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(o => o.ConfirmedAt ?? o.CreatedAt)
                    .ThenByDescending(o => o.OrderId)
                    .First());

        foreach (var item in projectItems)
        {
            if (!item.ProjectId.HasValue)
            {
                continue;
            }

            if (customerByProject.TryGetValue(item.ProjectId.Value, out var customer))
            {
                item.CustomerId = customer.CustomerId;
                item.CustomerName = customer.CustomerName;
            }

            if (orderByProject.TryGetValue(item.ProjectId.Value, out var order))
            {
                item.OrderId = order.OrderId;
                item.OrderCode = order.OrderCode;
                item.OrderFinalTotal = order.FinalTotalAmount;
                item.OrderPaidAmount = order.PaidAmount;
                item.OrderRemainingAmount = order.RemainingAmount;
            }
        }

        return projectItems;
    }

    private static AdminFinancialDrilldownBreakdownReadModel BuildPaymentTypeBreakdownFromProjectTotals(
        IReadOnlyList<AdminFinancialDrilldownItemReadModel> projectItems)
    {
        var buckets = new (string Key, decimal Amount, int Count)[]
        {
            (nameof(PaymentType.PROJECT_START_FEE), projectItems.Sum(i => i.ProjectStartFeeAmount ?? 0m),
                projectItems.Count(i => (i.ProjectStartFeeAmount ?? 0m) > 0m)),
            (nameof(PaymentType.DEPOSIT), projectItems.Sum(i => i.DepositAmount ?? 0m),
                projectItems.Count(i => (i.DepositAmount ?? 0m) > 0m)),
            (nameof(PaymentType.REMAINING_PAYMENT), projectItems.Sum(i => i.RemainingPaymentAmount ?? 0m),
                projectItems.Count(i => (i.RemainingPaymentAmount ?? 0m) > 0m)),
            (nameof(PaymentType.FULL_PAYMENT), projectItems.Sum(i => i.FullPaymentAmount ?? 0m),
                projectItems.Count(i => (i.FullPaymentAmount ?? 0m) > 0m))
        };

        return new AdminFinancialDrilldownBreakdownReadModel
        {
            Dimension = DimensionPaymentType,
            Items = buckets
                .Where(b => b.Amount > 0m || b.Count > 0)
                .Select(b => new AdminFinancialDrilldownBreakdownItemReadModel
                {
                    Key = b.Key,
                    Label = LabelPaymentType(b.Key),
                    Amount = b.Amount,
                    Count = b.Count
                })
                .OrderByDescending(x => x.Amount)
                .ThenBy(x => x.Key)
                .ToList()
        };
    }

    private async Task<AdminFinancialSummaryDrilldownReadModel> BuildActivePaymentDrilldownAsync(
        AdminFinancialSummaryDrilldownQueryReadModel query,
        DateTime utcNow,
        string currency,
        bool isOutstandingMetric,
        CancellationToken cancellationToken)
    {
        var baseQuery = ApplyPaymentDrilldownFilters(BuildActivePaymentQuery(utcNow, currency), query);
        var rows = await ProjectPaymentDrilldownRowsAsync(baseQuery, cancellationToken);
        var providerByPayment = await LoadLatestProvidersByPaymentAsync(
            rows.Select(r => r.PaymentId).ToList(),
            cancellationToken);

        var items = rows.Select(r =>
        {
            providerByPayment.TryGetValue(r.PaymentId, out var provider);
            return ToPaymentItem(r, provider, occurredAt: r.CreatedAt, utcNow, includeExpiredAt: true);
        }).ToList();

        items = FilterItemsByProvider(items, query);

        var breakdowns = new List<AdminFinancialDrilldownBreakdownReadModel>
        {
            BuildPaymentTypeBreakdown(items),
            BuildStatusBreakdown(items),
            BuildProjectBreakdown(items),
            BuildAgingBreakdown(items)
        };

        if (!isOutstandingMetric)
        {
            breakdowns.Insert(2, BuildProviderBreakdown(items));
        }

        var metric = isOutstandingMetric ? "OUTSTANDING" : "ACTIVE_PAYMENTS";
        return PageDrilldown(metric, items, breakdowns, query);
    }

    private async Task<AdminFinancialSummaryDrilldownReadModel> BuildContractedReceivableDrilldownAsync(
        AdminFinancialSummaryDrilldownQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var baseQuery = ApplyOrderDrilldownFilters(
            _dbContext.OrderSet.AsNoTracking()
                .Where(order =>
                    order.Status.HasValue &&
                    ActiveReceivableOrderStatuses.Contains(order.Status.Value) &&
                    order.RemainingAmount.HasValue &&
                    order.RemainingAmount.Value > 0m),
            query);

        var rows = await ProjectOrderDrilldownRowsAsync(baseQuery, cancellationToken);
        var items = rows.Select(r =>
        {
            var occurredAt = r.ConfirmedAt ?? r.CreatedAt;
            return ToOrderItem(r, amount: r.RemainingAmount ?? 0m, occurredAt, utcNow);
        }).ToList();

        return PageDrilldown(
            "CONTRACTED_RECEIVABLE",
            items,
            [
                BuildProjectBreakdown(items),
                BuildOrderStatusBreakdown(items),
                BuildAgingBreakdown(items)
            ],
            query);
    }

    private async Task<AdminFinancialSummaryDrilldownReadModel> BuildOrderValueDrilldownAsync(
        AdminFinancialSummaryDrilldownQueryReadModel query,
        DateTime fromUtc,
        DateTime toUtcExclusive,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var baseQuery = ApplyOrderDrilldownFilters(
            _dbContext.OrderSet.AsNoTracking()
                .Where(order =>
                    order.Status != OrderStatus.CANCELLED &&
                    order.ConfirmedAt.HasValue &&
                    order.ConfirmedAt.Value >= fromUtc &&
                    order.ConfirmedAt.Value < toUtcExclusive),
            query);

        var rows = await ProjectOrderDrilldownRowsAsync(baseQuery, cancellationToken);
        var items = rows
            .Select(r => ToOrderItem(r, amount: r.FinalTotalAmount, occurredAt: r.ConfirmedAt, utcNow))
            .ToList();

        return PageDrilldown(
            "ORDER_VALUE",
            items,
            [
                BuildProjectBreakdown(items),
                BuildOrderStatusBreakdown(items)
            ],
            query);
    }

    private async Task<AdminFinancialSummaryDrilldownReadModel> BuildFailedTransactionsDrilldownAsync(
        AdminFinancialSummaryDrilldownQueryReadModel query,
        DateTime fromUtc,
        DateTime toUtcExclusive,
        DateTime utcNow,
        string currency,
        CancellationToken cancellationToken)
    {
        var baseQuery = _dbContext.PaymentTransactionSet.AsNoTracking()
            .Where(t =>
                t.Status == PaymentTransactionStatus.FAILED &&
                t.CreatedAt.HasValue &&
                t.CreatedAt.Value >= fromUtc &&
                t.CreatedAt.Value < toUtcExclusive &&
                t.Currency == currency);

        if (query.ProjectId.HasValue)
        {
            baseQuery = baseQuery.Where(t => t.ProjectId == query.ProjectId.Value);
        }

        if (query.Provider.HasValue)
        {
            baseQuery = baseQuery.Where(t => t.PaymentProvider == query.Provider.Value);
        }

        var rows = await (
            from txn in baseQuery
            join payment in _dbContext.PaymentSet.AsNoTracking() on txn.PaymentId equals payment.PaymentId
            join project in _dbContext.ProjectSet.AsNoTracking() on payment.ProjectId equals project.ProjectId
            join order in _dbContext.OrderSet.AsNoTracking() on payment.OrderId equals order.OrderId into orders
            from order in orders.DefaultIfEmpty()
            select new
            {
                txn.PaymentTransactionId,
                txn.PaymentId,
                payment.PaymentCode,
                payment.ProjectId,
                project.ProjectCode,
                project.ProjectName,
                payment.OrderId,
                OrderCode = order != null ? order.OrderCode : null,
                payment.PaymentType,
                txn.PaymentProvider,
                txn.Amount,
                txn.FailureReason,
                txn.CreatedAt
            }).ToListAsync(cancellationToken);

        if (query.PaymentType.HasValue)
        {
            rows = rows.Where(r => r.PaymentType == query.PaymentType.Value).ToList();
        }

        var items = rows.Select(r => new AdminFinancialDrilldownItemReadModel
        {
            ResourceType = ResourceTransaction,
            ProjectId = r.ProjectId,
            ProjectCode = r.ProjectCode,
            ProjectName = r.ProjectName,
            OrderId = r.OrderId,
            OrderCode = r.OrderCode,
            PaymentId = r.PaymentId,
            PaymentCode = r.PaymentCode,
            TransactionId = r.PaymentTransactionId,
            PaymentType = r.PaymentType?.ToString(),
            Status = PaymentTransactionStatus.FAILED.ToString(),
            Provider = r.PaymentProvider?.ToString() ?? UnknownKey,
            Amount = r.Amount,
            OccurredAt = r.CreatedAt,
            FailureReason = r.FailureReason,
            AgeDays = CalculateAgeDays(r.CreatedAt, utcNow)
        }).ToList();

        return PageDrilldown(
            "FAILED_TRANSACTIONS",
            items,
            [
                BuildProviderBreakdown(items),
                BuildBreakdown(
                    DimensionFailureReason,
                    items,
                    i => ResolveFailureReasonKey(i.FailureReason),
                    key => key),
                BuildPaymentTypeBreakdown(items),
                BuildProjectBreakdown(items)
            ],
            query);
    }

    private async Task<List<PaymentDrilldownRow>> ProjectPaymentDrilldownRowsAsync(
        IQueryable<Payment> baseQuery,
        CancellationToken cancellationToken)
    {
        return await (
            from payment in baseQuery
            join project in _dbContext.ProjectSet.AsNoTracking() on payment.ProjectId equals project.ProjectId
            join order in _dbContext.OrderSet.AsNoTracking() on payment.OrderId equals order.OrderId into orders
            from order in orders.DefaultIfEmpty()
            select new PaymentDrilldownRow(
                payment.PaymentId,
                payment.PaymentCode,
                payment.ProjectId,
                project.ProjectCode,
                project.ProjectName,
                payment.OrderId,
                order != null ? order.OrderCode : null,
                order != null ? order.Status : null,
                payment.PaymentType,
                payment.Status,
                payment.Amount,
                payment.PaidAt,
                payment.CreatedAt,
                payment.ExpiredAt)).ToListAsync(cancellationToken);
    }

    private async Task<List<OrderDrilldownRow>> ProjectOrderDrilldownRowsAsync(
        IQueryable<Order> baseQuery,
        CancellationToken cancellationToken)
    {
        return await (
            from order in baseQuery
            join project in _dbContext.ProjectSet.AsNoTracking() on order.ProjectId equals project.ProjectId
            select new OrderDrilldownRow(
                order.OrderId,
                order.OrderCode,
                order.ProjectId,
                project.ProjectCode,
                project.ProjectName,
                order.Status,
                order.FinalTotalAmount,
                order.PaidAmount,
                order.RemainingAmount,
                order.ConfirmedAt,
                order.CreatedAt)).ToListAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, PaymentProvider?>> LoadSuccessProvidersByPaymentAsync(
        List<Guid> paymentIds,
        CancellationToken cancellationToken)
    {
        if (paymentIds.Count == 0)
        {
            return new Dictionary<Guid, PaymentProvider?>();
        }

        var providers = await _dbContext.PaymentTransactionSet.AsNoTracking()
            .Where(t => paymentIds.Contains(t.PaymentId) && t.Status == PaymentTransactionStatus.SUCCESS)
            .Select(t => new { t.PaymentId, t.PaymentProvider, SortAt = t.ConfirmedAt ?? t.CreatedAt })
            .ToListAsync(cancellationToken);

        return ToLatestProviderMap(providers.Select(t => (t.PaymentId, t.PaymentProvider, t.SortAt)));
    }

    private async Task<Dictionary<Guid, PaymentProvider?>> LoadLatestProvidersByPaymentAsync(
        List<Guid> paymentIds,
        CancellationToken cancellationToken)
    {
        if (paymentIds.Count == 0)
        {
            return new Dictionary<Guid, PaymentProvider?>();
        }

        var attempts = await _dbContext.PaymentTransactionSet.AsNoTracking()
            .Where(t => paymentIds.Contains(t.PaymentId))
            .Select(t => new { t.PaymentId, t.PaymentProvider, t.CreatedAt })
            .ToListAsync(cancellationToken);

        return ToLatestProviderMap(attempts.Select(t => (t.PaymentId, t.PaymentProvider, t.CreatedAt)));
    }

    private static Dictionary<Guid, PaymentProvider?> ToLatestProviderMap(
        IEnumerable<(Guid PaymentId, PaymentProvider? Provider, DateTime? SortAt)> rows)
    {
        return rows
            .GroupBy(t => t.PaymentId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(t => t.SortAt).Select(t => t.Provider).FirstOrDefault());
    }

    private static IQueryable<Payment> ApplyPaymentDrilldownFilters(
        IQueryable<Payment> queryable,
        AdminFinancialSummaryDrilldownQueryReadModel query)
    {
        if (query.ProjectId.HasValue)
        {
            queryable = queryable.Where(p => p.ProjectId == query.ProjectId.Value);
        }

        if (query.PaymentType.HasValue)
        {
            queryable = queryable.Where(p => p.PaymentType == query.PaymentType.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<PaymentStatus>(query.Status, ignoreCase: true, out var statusFilter))
        {
            queryable = queryable.Where(p => p.Status == statusFilter);
        }

        return queryable;
    }

    private static IQueryable<Order> ApplyOrderDrilldownFilters(
        IQueryable<Order> queryable,
        AdminFinancialSummaryDrilldownQueryReadModel query)
    {
        if (query.ProjectId.HasValue)
        {
            queryable = queryable.Where(o => o.ProjectId == query.ProjectId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<OrderStatus>(query.Status, ignoreCase: true, out var orderStatus))
        {
            queryable = queryable.Where(o => o.Status == orderStatus);
        }

        return queryable;
    }

    private static List<AdminFinancialDrilldownItemReadModel> FilterItemsByProvider(
        List<AdminFinancialDrilldownItemReadModel> items,
        AdminFinancialSummaryDrilldownQueryReadModel query)
    {
        if (!query.Provider.HasValue)
        {
            return items;
        }

        var providerKey = query.Provider.Value.ToString();
        return items
            .Where(i => string.Equals(i.Provider, providerKey, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static AdminFinancialDrilldownItemReadModel ToPaymentItem(
        PaymentDrilldownRow row,
        PaymentProvider? provider,
        DateTime? occurredAt,
        DateTime utcNow,
        bool includeExpiredAt = false)
    {
        return new AdminFinancialDrilldownItemReadModel
        {
            ResourceType = ResourcePayment,
            ProjectId = row.ProjectId,
            ProjectCode = row.ProjectCode,
            ProjectName = row.ProjectName,
            OrderId = row.OrderId,
            OrderCode = row.OrderCode,
            OrderStatus = row.OrderStatus?.ToString(),
            PaymentId = row.PaymentId,
            PaymentCode = row.PaymentCode,
            PaymentType = row.PaymentType?.ToString(),
            Status = row.Status?.ToString(),
            Provider = provider?.ToString() ?? UnknownKey,
            Amount = row.Amount,
            OccurredAt = occurredAt,
            ExpiredAt = includeExpiredAt ? row.ExpiredAt : null,
            AgeDays = CalculateAgeDays(occurredAt, utcNow)
        };
    }

    private static AdminFinancialDrilldownItemReadModel ToOrderItem(
        OrderDrilldownRow row,
        decimal amount,
        DateTime? occurredAt,
        DateTime utcNow)
    {
        return new AdminFinancialDrilldownItemReadModel
        {
            ResourceType = ResourceOrder,
            ProjectId = row.ProjectId,
            ProjectCode = row.ProjectCode,
            ProjectName = row.ProjectName,
            OrderId = row.OrderId,
            OrderCode = row.OrderCode,
            OrderStatus = row.Status?.ToString(),
            Status = row.Status?.ToString(),
            Amount = amount,
            PaidAmount = row.PaidAmount,
            RemainingAmount = row.RemainingAmount,
            OccurredAt = occurredAt,
            AgeDays = CalculateAgeDays(occurredAt, utcNow)
        };
    }

    private static AdminFinancialSummaryDrilldownReadModel PageDrilldown(
        string metric,
        List<AdminFinancialDrilldownItemReadModel> items,
        IReadOnlyList<AdminFinancialDrilldownBreakdownReadModel> breakdowns,
        AdminFinancialSummaryDrilldownQueryReadModel query,
        decimal? totalAmountOverride = null)
    {
        var sorted = SortDrilldownItems(items, query.SortBy, query.SortDirection).ToList();
        var totalItems = sorted.Count;
        var pageItems = sorted
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return new AdminFinancialSummaryDrilldownReadModel
        {
            Metric = metric,
            TotalAmount = totalAmountOverride ?? items.Sum(i => i.Amount),
            TotalCount = totalItems,
            Breakdowns = breakdowns,
            Items = pageItems,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalItems = totalItems
        };
    }

    private static AdminFinancialDrilldownBreakdownReadModel BuildPaymentTypeBreakdown(
        IReadOnlyList<AdminFinancialDrilldownItemReadModel> items) =>
        BuildBreakdown(DimensionPaymentType, items, i => i.PaymentType ?? UnknownKey, LabelPaymentType);

    private static AdminFinancialDrilldownBreakdownReadModel BuildProviderBreakdown(
        IReadOnlyList<AdminFinancialDrilldownItemReadModel> items) =>
        BuildBreakdown(DimensionProvider, items, i => i.Provider ?? UnknownKey, key => key);

    private static AdminFinancialDrilldownBreakdownReadModel BuildStatusBreakdown(
        IReadOnlyList<AdminFinancialDrilldownItemReadModel> items) =>
        BuildBreakdown(DimensionStatus, items, i => i.Status ?? UnknownKey, key => key);

    private static AdminFinancialDrilldownBreakdownReadModel BuildOrderStatusBreakdown(
        IReadOnlyList<AdminFinancialDrilldownItemReadModel> items) =>
        BuildBreakdown(DimensionOrderStatus, items, i => i.OrderStatus ?? UnknownKey, key => key);

    private static AdminFinancialDrilldownBreakdownReadModel BuildAgingBreakdown(
        IReadOnlyList<AdminFinancialDrilldownItemReadModel> items) =>
        BuildBreakdown(DimensionAging, items, i => AgingBucketKey(i.AgeDays), LabelAging);

    private static AdminFinancialDrilldownBreakdownReadModel BuildProjectBreakdown(
        IReadOnlyList<AdminFinancialDrilldownItemReadModel> items)
    {
        var projectCodeById = items
            .Where(i => i.ProjectId.HasValue)
            .GroupBy(i => i.ProjectId!.Value.ToString())
            .ToDictionary(g => g.Key, g => g.First().ProjectCode ?? g.Key);

        return BuildBreakdown(
            DimensionProject,
            items,
            i => i.ProjectId?.ToString() ?? UnknownKey,
            key => projectCodeById.TryGetValue(key, out var code) ? code : key);
    }

    private static AdminFinancialDrilldownBreakdownReadModel BuildBreakdown(
        string dimension,
        IReadOnlyList<AdminFinancialDrilldownItemReadModel> items,
        Func<AdminFinancialDrilldownItemReadModel, string> keySelector,
        Func<string, string> labelSelector)
    {
        var groups = items
            .GroupBy(keySelector)
            .Select(g => new AdminFinancialDrilldownBreakdownItemReadModel
            {
                Key = g.Key,
                Label = labelSelector(g.Key),
                Amount = g.Sum(x => x.Amount),
                Count = g.Count()
            })
            .OrderByDescending(x => x.Amount)
            .ThenBy(x => x.Key)
            .ToList();

        return new AdminFinancialDrilldownBreakdownReadModel
        {
            Dimension = dimension,
            Items = groups
        };
    }

    private static IEnumerable<AdminFinancialDrilldownItemReadModel> SortDrilldownItems(
        IEnumerable<AdminFinancialDrilldownItemReadModel> items,
        string sortBy,
        string sortDirection)
    {
        var desc = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        return (sortBy?.Trim().ToLowerInvariant()) switch
        {
            "amount" or "totalcollectedamount" => OrderByKey(items, i => i.TotalCollectedAmount ?? i.Amount, desc),
            "agedays" => OrderByKey(items, i => i.AgeDays, desc),
            "projectcode" => OrderByKey(items, i => i.ProjectCode, desc),
            "paymentcode" => OrderByKey(items, i => i.PaymentCode, desc),
            "ordercode" => OrderByKey(items, i => i.OrderCode, desc),
            "paymentcount" => OrderByKey(items, i => i.PaymentCount ?? 0, desc),
            "lastpaidat" => OrderByKey(items, i => i.LastPaidAt ?? i.OccurredAt, desc, thenByAmount: true),
            _ => OrderByKey(items, i => i.OccurredAt, desc, thenByAmount: true)
        };
    }

    private static IOrderedEnumerable<AdminFinancialDrilldownItemReadModel> OrderByKey<TKey>(
        IEnumerable<AdminFinancialDrilldownItemReadModel> items,
        Func<AdminFinancialDrilldownItemReadModel, TKey> keySelector,
        bool desc,
        bool thenByAmount = false)
    {
        var ordered = desc
            ? items.OrderByDescending(keySelector)
            : items.OrderBy(keySelector);

        if (thenByAmount)
        {
            return desc
                ? ordered.ThenByDescending(i => i.Amount)
                : ordered.ThenBy(i => i.Amount);
        }

        return desc
            ? ordered.ThenByDescending(i => i.OccurredAt)
            : ordered.ThenBy(i => i.OccurredAt);
    }

    private static int CalculateAgeDays(DateTime? occurredAt, DateTime utcNow)
    {
        if (!occurredAt.HasValue)
        {
            return 0;
        }

        return Math.Max(0, (utcNow.Date - occurredAt.Value.Date).Days);
    }

    private static string AgingBucketKey(int ageDays) => ageDays switch
    {
        <= 3 => Aging0To3,
        <= 7 => Aging4To7,
        <= 14 => Aging8To14,
        _ => AgingOver14
    };

    private static string ResolveFailureReasonKey(string? failureReason) =>
        string.IsNullOrWhiteSpace(failureReason) ? UnknownKey : failureReason;

    private static string LabelAging(string key) => key switch
    {
        Aging0To3 => "0-3 days",
        Aging4To7 => "4-7 days",
        Aging8To14 => "8-14 days",
        AgingOver14 => ">14 days",
        _ => key
    };

    private static string LabelPaymentType(string key) => key switch
    {
        nameof(PaymentType.PROJECT_START_FEE) => "Project start fee",
        nameof(PaymentType.DEPOSIT) => "Deposit",
        nameof(PaymentType.REMAINING_PAYMENT) => "Remaining payment",
        nameof(PaymentType.FULL_PAYMENT) => "Full payment",
        nameof(PaymentType.REFUND) => "Refund",
        nameof(PaymentType.OTHER) => "Other",
        _ => key
    };

    private sealed record PaymentDrilldownRow(
        Guid PaymentId,
        string? PaymentCode,
        Guid? ProjectId,
        string? ProjectCode,
        string? ProjectName,
        Guid? OrderId,
        string? OrderCode,
        OrderStatus? OrderStatus,
        PaymentType? PaymentType,
        PaymentStatus? Status,
        decimal Amount,
        DateTime? PaidAt,
        DateTime? CreatedAt,
        DateTime? ExpiredAt);

    private sealed record OrderDrilldownRow(
        Guid OrderId,
        string? OrderCode,
        Guid? ProjectId,
        string? ProjectCode,
        string? ProjectName,
        OrderStatus? Status,
        decimal FinalTotalAmount,
        decimal? PaidAmount,
        decimal? RemainingAmount,
        DateTime? ConfirmedAt,
        DateTime? CreatedAt);
}
