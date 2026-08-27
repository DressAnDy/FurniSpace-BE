#nullable enable

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
    private const string Aging0To3 = "0_3";
    private const string Aging4To7 = "4_7";
    private const string Aging8To14 = "8_14";
    private const string AgingOver14 = "OVER_14";
    private const string ProviderUnknown = "UNKNOWN";

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
        var baseQuery = _dbContext.PaymentSet.AsNoTracking()
            .Where(payment =>
                payment.Status == PaymentStatus.PAID &&
                payment.PaymentType.HasValue &&
                canonicalPaymentTypes.Contains(payment.PaymentType.Value) &&
                payment.PaidAt.HasValue &&
                payment.PaidAt.Value >= fromUtc &&
                payment.PaidAt.Value < toUtcExclusive &&
                payment.Currency == currency);

        if (query.ProjectId.HasValue)
        {
            baseQuery = baseQuery.Where(p => p.ProjectId == query.ProjectId.Value);
        }

        if (query.PaymentType.HasValue)
        {
            baseQuery = baseQuery.Where(p => p.PaymentType == query.PaymentType.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<PaymentStatus>(query.Status, ignoreCase: true, out var statusFilter))
        {
            baseQuery = baseQuery.Where(p => p.Status == statusFilter);
        }

        var rows = await (
            from payment in baseQuery
            join project in _dbContext.ProjectSet.AsNoTracking() on payment.ProjectId equals project.ProjectId
            join order in _dbContext.OrderSet.AsNoTracking() on payment.OrderId equals order.OrderId into orders
            from order in orders.DefaultIfEmpty()
            select new
            {
                payment.PaymentId,
                payment.PaymentCode,
                payment.ProjectId,
                project.ProjectCode,
                project.ProjectName,
                payment.OrderId,
                OrderCode = order != null ? order.OrderCode : null,
                OrderStatus = order != null ? order.Status : null,
                payment.PaymentType,
                payment.Status,
                payment.Amount,
                OccurredAt = payment.PaidAt
            }).ToListAsync(cancellationToken);

        var paymentIds = rows.Select(r => r.PaymentId).ToList();
        var providers = await _dbContext.PaymentTransactionSet.AsNoTracking()
            .Where(t => paymentIds.Contains(t.PaymentId) && t.Status == PaymentTransactionStatus.SUCCESS)
            .Select(t => new { t.PaymentId, t.PaymentProvider, t.ConfirmedAt, t.CreatedAt })
            .ToListAsync(cancellationToken);

        var providerByPayment = providers
            .GroupBy(t => t.PaymentId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(t => t.ConfirmedAt ?? t.CreatedAt)
                    .Select(t => t.PaymentProvider)
                    .FirstOrDefault());

        var items = rows.Select(r =>
        {
            providerByPayment.TryGetValue(r.PaymentId, out var provider);
            return new AdminFinancialDrilldownItemReadModel
            {
                ResourceType = ResourcePayment,
                ProjectId = r.ProjectId,
                ProjectCode = r.ProjectCode,
                ProjectName = r.ProjectName,
                OrderId = r.OrderId,
                OrderCode = r.OrderCode,
                OrderStatus = r.OrderStatus?.ToString(),
                PaymentId = r.PaymentId,
                PaymentCode = r.PaymentCode,
                PaymentType = r.PaymentType?.ToString(),
                Status = r.Status?.ToString(),
                Provider = provider?.ToString() ?? ProviderUnknown,
                Amount = r.Amount,
                OccurredAt = r.OccurredAt,
                AgeDays = CalculateAgeDays(r.OccurredAt, utcNow)
            };
        }).ToList();

        if (query.Provider.HasValue)
        {
            var providerKey = query.Provider.Value.ToString();
            items = items.Where(i => string.Equals(i.Provider, providerKey, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var totalAmount = items.Sum(i => i.Amount);
        var breakdowns = new List<AdminFinancialDrilldownBreakdownReadModel>
        {
            BuildBreakdown(DimensionPaymentType, items, i => i.PaymentType ?? "UNKNOWN", LabelPaymentType),
            BuildBreakdown(DimensionProject, items, i => i.ProjectId?.ToString() ?? "UNKNOWN", key =>
                items.FirstOrDefault(i => i.ProjectId?.ToString() == key)?.ProjectCode ?? key),
            BuildBreakdown(DimensionProvider, items, i => i.Provider ?? ProviderUnknown, key => key)
        };

        return PageDrilldown("COLLECTED", totalAmount, items, breakdowns, query);
    }

    private async Task<AdminFinancialSummaryDrilldownReadModel> BuildActivePaymentDrilldownAsync(
        AdminFinancialSummaryDrilldownQueryReadModel query,
        DateTime utcNow,
        string currency,
        bool isOutstandingMetric,
        CancellationToken cancellationToken)
    {
        var baseQuery = BuildActivePaymentQuery(utcNow, currency);
        if (query.ProjectId.HasValue)
        {
            baseQuery = baseQuery.Where(p => p.ProjectId == query.ProjectId.Value);
        }

        if (query.PaymentType.HasValue)
        {
            baseQuery = baseQuery.Where(p => p.PaymentType == query.PaymentType.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<PaymentStatus>(query.Status, ignoreCase: true, out var statusFilter))
        {
            baseQuery = baseQuery.Where(p => p.Status == statusFilter);
        }

        var rows = await (
            from payment in baseQuery
            join project in _dbContext.ProjectSet.AsNoTracking() on payment.ProjectId equals project.ProjectId
            join order in _dbContext.OrderSet.AsNoTracking() on payment.OrderId equals order.OrderId into orders
            from order in orders.DefaultIfEmpty()
            select new
            {
                payment.PaymentId,
                payment.PaymentCode,
                payment.ProjectId,
                project.ProjectCode,
                project.ProjectName,
                payment.OrderId,
                OrderCode = order != null ? order.OrderCode : null,
                payment.PaymentType,
                payment.Status,
                payment.Amount,
                payment.CreatedAt,
                payment.ExpiredAt
            }).ToListAsync(cancellationToken);

        var paymentIds = rows.Select(r => r.PaymentId).ToList();
        var latestAttempts = await _dbContext.PaymentTransactionSet.AsNoTracking()
            .Where(t => paymentIds.Contains(t.PaymentId))
            .Select(t => new { t.PaymentId, t.PaymentProvider, t.CreatedAt })
            .ToListAsync(cancellationToken);
        var providerByPayment = latestAttempts
            .GroupBy(t => t.PaymentId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(t => t.CreatedAt).Select(t => t.PaymentProvider).FirstOrDefault());

        var items = rows.Select(r =>
        {
            providerByPayment.TryGetValue(r.PaymentId, out var provider);
            return new AdminFinancialDrilldownItemReadModel
            {
                ResourceType = ResourcePayment,
                ProjectId = r.ProjectId,
                ProjectCode = r.ProjectCode,
                ProjectName = r.ProjectName,
                OrderId = r.OrderId,
                OrderCode = r.OrderCode,
                PaymentId = r.PaymentId,
                PaymentCode = r.PaymentCode,
                PaymentType = r.PaymentType?.ToString(),
                Status = r.Status?.ToString(),
                Provider = provider?.ToString() ?? ProviderUnknown,
                Amount = r.Amount,
                OccurredAt = r.CreatedAt,
                ExpiredAt = r.ExpiredAt,
                AgeDays = CalculateAgeDays(r.CreatedAt, utcNow)
            };
        }).ToList();

        if (query.Provider.HasValue)
        {
            var providerKey = query.Provider.Value.ToString();
            items = items.Where(i => string.Equals(i.Provider, providerKey, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var totalAmount = items.Sum(i => i.Amount);
        var breakdowns = new List<AdminFinancialDrilldownBreakdownReadModel>
        {
            BuildBreakdown(DimensionPaymentType, items, i => i.PaymentType ?? "UNKNOWN", LabelPaymentType),
            BuildBreakdown(DimensionStatus, items, i => i.Status ?? "UNKNOWN", key => key),
            BuildBreakdown(DimensionProject, items, i => i.ProjectId?.ToString() ?? "UNKNOWN", key =>
                items.FirstOrDefault(i => i.ProjectId?.ToString() == key)?.ProjectCode ?? key),
            BuildBreakdown(DimensionAging, items, i => AgingBucketKey(i.AgeDays), LabelAging)
        };

        if (!isOutstandingMetric)
        {
            breakdowns.Insert(2, BuildBreakdown(DimensionProvider, items, i => i.Provider ?? ProviderUnknown, key => key));
        }

        var metric = isOutstandingMetric ? "OUTSTANDING" : "ACTIVE_PAYMENTS";
        return PageDrilldown(metric, totalAmount, items, breakdowns, query);
    }

    private async Task<AdminFinancialSummaryDrilldownReadModel> BuildContractedReceivableDrilldownAsync(
        AdminFinancialSummaryDrilldownQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var baseQuery = _dbContext.OrderSet.AsNoTracking()
            .Where(order =>
                order.Status.HasValue &&
                ActiveReceivableOrderStatuses.Contains(order.Status.Value) &&
                order.RemainingAmount.HasValue &&
                order.RemainingAmount.Value > 0m);

        if (query.ProjectId.HasValue)
        {
            baseQuery = baseQuery.Where(o => o.ProjectId == query.ProjectId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<OrderStatus>(query.Status, ignoreCase: true, out var orderStatus))
        {
            baseQuery = baseQuery.Where(o => o.Status == orderStatus);
        }

        var rows = await (
            from order in baseQuery
            join project in _dbContext.ProjectSet.AsNoTracking() on order.ProjectId equals project.ProjectId
            select new
            {
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
                order.CreatedAt
            }).ToListAsync(cancellationToken);

        var items = rows.Select(r =>
        {
            var occurredAt = r.ConfirmedAt ?? r.CreatedAt;
            return new AdminFinancialDrilldownItemReadModel
            {
                ResourceType = ResourceOrder,
                ProjectId = r.ProjectId,
                ProjectCode = r.ProjectCode,
                ProjectName = r.ProjectName,
                OrderId = r.OrderId,
                OrderCode = r.OrderCode,
                OrderStatus = r.Status?.ToString(),
                Status = r.Status?.ToString(),
                Amount = r.RemainingAmount ?? 0m,
                PaidAmount = r.PaidAmount,
                RemainingAmount = r.RemainingAmount,
                OccurredAt = occurredAt,
                AgeDays = CalculateAgeDays(occurredAt, utcNow)
            };
        }).ToList();

        var totalAmount = items.Sum(i => i.Amount);
        var breakdowns = new List<AdminFinancialDrilldownBreakdownReadModel>
        {
            BuildBreakdown(DimensionProject, items, i => i.ProjectId?.ToString() ?? "UNKNOWN", key =>
                items.FirstOrDefault(i => i.ProjectId?.ToString() == key)?.ProjectCode ?? key),
            BuildBreakdown(DimensionOrderStatus, items, i => i.OrderStatus ?? "UNKNOWN", key => key),
            BuildBreakdown(DimensionAging, items, i => AgingBucketKey(i.AgeDays), LabelAging)
        };

        return PageDrilldown("CONTRACTED_RECEIVABLE", totalAmount, items, breakdowns, query);
    }

    private async Task<AdminFinancialSummaryDrilldownReadModel> BuildOrderValueDrilldownAsync(
        AdminFinancialSummaryDrilldownQueryReadModel query,
        DateTime fromUtc,
        DateTime toUtcExclusive,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var baseQuery = _dbContext.OrderSet.AsNoTracking()
            .Where(order =>
                order.Status != OrderStatus.CANCELLED &&
                order.ConfirmedAt.HasValue &&
                order.ConfirmedAt.Value >= fromUtc &&
                order.ConfirmedAt.Value < toUtcExclusive);

        if (query.ProjectId.HasValue)
        {
            baseQuery = baseQuery.Where(o => o.ProjectId == query.ProjectId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<OrderStatus>(query.Status, ignoreCase: true, out var orderStatus))
        {
            baseQuery = baseQuery.Where(o => o.Status == orderStatus);
        }

        var rows = await (
            from order in baseQuery
            join project in _dbContext.ProjectSet.AsNoTracking() on order.ProjectId equals project.ProjectId
            select new
            {
                order.OrderId,
                order.OrderCode,
                order.ProjectId,
                project.ProjectCode,
                project.ProjectName,
                order.Status,
                order.FinalTotalAmount,
                order.PaidAmount,
                order.RemainingAmount,
                order.ConfirmedAt
            }).ToListAsync(cancellationToken);

        var items = rows.Select(r => new AdminFinancialDrilldownItemReadModel
        {
            ResourceType = ResourceOrder,
            ProjectId = r.ProjectId,
            ProjectCode = r.ProjectCode,
            ProjectName = r.ProjectName,
            OrderId = r.OrderId,
            OrderCode = r.OrderCode,
            OrderStatus = r.Status?.ToString(),
            Status = r.Status?.ToString(),
            Amount = r.FinalTotalAmount,
            PaidAmount = r.PaidAmount,
            RemainingAmount = r.RemainingAmount,
            OccurredAt = r.ConfirmedAt,
            AgeDays = CalculateAgeDays(r.ConfirmedAt, utcNow)
        }).ToList();

        var totalAmount = items.Sum(i => i.Amount);
        var breakdowns = new List<AdminFinancialDrilldownBreakdownReadModel>
        {
            BuildBreakdown(DimensionProject, items, i => i.ProjectId?.ToString() ?? "UNKNOWN", key =>
                items.FirstOrDefault(i => i.ProjectId?.ToString() == key)?.ProjectCode ?? key),
            BuildBreakdown(DimensionOrderStatus, items, i => i.OrderStatus ?? "UNKNOWN", key => key)
        };

        return PageDrilldown("ORDER_VALUE", totalAmount, items, breakdowns, query);
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
            Provider = r.PaymentProvider?.ToString() ?? ProviderUnknown,
            Amount = r.Amount,
            OccurredAt = r.CreatedAt,
            FailureReason = r.FailureReason,
            AgeDays = CalculateAgeDays(r.CreatedAt, utcNow)
        }).ToList();

        var totalAmount = items.Sum(i => i.Amount);
        var breakdowns = new List<AdminFinancialDrilldownBreakdownReadModel>
        {
            BuildBreakdown(DimensionProvider, items, i => i.Provider ?? ProviderUnknown, key => key),
            BuildBreakdown(DimensionFailureReason, items, i => string.IsNullOrWhiteSpace(i.FailureReason) ? "UNKNOWN" : i.FailureReason!, key => key),
            BuildBreakdown(DimensionPaymentType, items, i => i.PaymentType ?? "UNKNOWN", LabelPaymentType),
            BuildBreakdown(DimensionProject, items, i => i.ProjectId?.ToString() ?? "UNKNOWN", key =>
                items.FirstOrDefault(i => i.ProjectId?.ToString() == key)?.ProjectCode ?? key)
        };

        return PageDrilldown("FAILED_TRANSACTIONS", totalAmount, items, breakdowns, query);
    }

    private static AdminFinancialSummaryDrilldownReadModel PageDrilldown(
        string metric,
        decimal totalAmount,
        List<AdminFinancialDrilldownItemReadModel> items,
        IReadOnlyList<AdminFinancialDrilldownBreakdownReadModel> breakdowns,
        AdminFinancialSummaryDrilldownQueryReadModel query)
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
            TotalAmount = totalAmount,
            TotalCount = totalItems,
            Breakdowns = breakdowns,
            Items = pageItems,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalItems = totalItems
        };
    }

    private static AdminFinancialDrilldownBreakdownReadModel BuildBreakdown(
        string dimension,
        IReadOnlyList<AdminFinancialDrilldownItemReadModel> items,
        Func<AdminFinancialDrilldownItemReadModel, string> keySelector,
        Func<string, string> labelSelector)
    {
        var totalAmount = items.Sum(i => i.Amount);
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

        // Attach percentage in service layer; store amount/count here.
        _ = totalAmount;
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
            "amount" => desc
                ? items.OrderByDescending(i => i.Amount).ThenByDescending(i => i.OccurredAt)
                : items.OrderBy(i => i.Amount).ThenBy(i => i.OccurredAt),
            "agedays" => desc
                ? items.OrderByDescending(i => i.AgeDays).ThenByDescending(i => i.OccurredAt)
                : items.OrderBy(i => i.AgeDays).ThenBy(i => i.OccurredAt),
            "projectcode" => desc
                ? items.OrderByDescending(i => i.ProjectCode).ThenByDescending(i => i.OccurredAt)
                : items.OrderBy(i => i.ProjectCode).ThenBy(i => i.OccurredAt),
            "paymentcode" => desc
                ? items.OrderByDescending(i => i.PaymentCode).ThenByDescending(i => i.OccurredAt)
                : items.OrderBy(i => i.PaymentCode).ThenBy(i => i.OccurredAt),
            "ordercode" => desc
                ? items.OrderByDescending(i => i.OrderCode).ThenByDescending(i => i.OccurredAt)
                : items.OrderBy(i => i.OrderCode).ThenBy(i => i.OccurredAt),
            _ => desc
                ? items.OrderByDescending(i => i.OccurredAt).ThenByDescending(i => i.Amount)
                : items.OrderBy(i => i.OccurredAt).ThenBy(i => i.Amount)
        };
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
}
