#nullable enable

using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Financial;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class FinancialDiscountReadRepository : IFinancialDiscountReadRepository
{
    private readonly AppDbContext _dbContext;

    public FinancialDiscountReadRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminFinancialDiscountSummaryReadModel> GetSummaryAsync(
        AdminFinancialDiscountQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        var rows = await BuildOrderMetricsQuery(query).ToListAsync(cancellationToken);
        return AggregateSummary(rows);
    }

    public Task<IReadOnlyList<AdminFinancialDiscountOrderMetricsReadModel>> GetOrderMetricsAsync(
        AdminFinancialDiscountQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return BuildPagedOrderMetricsQuery(query).ToListAsync(cancellationToken)
            .ContinueWith<IReadOnlyList<AdminFinancialDiscountOrderMetricsReadModel>>(
                task => task.Result,
                cancellationToken);
    }

    public Task<int> CountOrderMetricsAsync(
        AdminFinancialDiscountQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return BuildOrderMetricsQuery(query).CountAsync(cancellationToken);
    }

    public Task<AdminFinancialDiscountOrderMetricsReadModel?> GetOrderMetricsByIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var query = new AdminFinancialDiscountQueryReadModel
        {
            FromUtc = DateTime.MinValue,
            ToUtcExclusive = DateTime.MaxValue,
            OrderId = orderId,
            Page = 1,
            PageSize = 1
        };
        return BuildOrderMetricsQuery(query).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminFinancialDiscountOrderItemReadModel>> GetOrderItemsAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.OrderItemSet.AsNoTracking()
            .Where(item => item.OrderId == orderId)
            .OrderBy(item => item.ProductNameSnapshot)
            .ThenBy(item => item.OrderItemId)
            .Select(item => new AdminFinancialDiscountOrderItemReadModel
            {
                OrderItemId = item.OrderItemId,
                ProductName = item.ProductNameSnapshot,
                ProductVersionName = item.ProductVersionNameSnapshot,
                Quantity = item.Quantity ?? 0,
                UnitPrice = item.UnitPrice ?? 0m,
                LineGrossAmount = (item.Quantity ?? 0) * (item.UnitPrice ?? 0m),
                DiscountAmount = item.DiscountAmount ?? 0m,
                SubtotalAmount = item.SubtotalAmount ?? 0m
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminFinancialDiscountTrendBucketReadModel>> GetTrendAsync(
        AdminFinancialDiscountQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        var rows = await BuildOrderMetricsQuery(query).ToListAsync(cancellationToken);
        return rows
            .Where(row => row.ConfirmedAt.HasValue)
            .Select(row => (Row: row, ConfirmedAt: row.ConfirmedAt.Value))
            .GroupBy(entry => new { entry.ConfirmedAt.Year, entry.ConfirmedAt.Month })
            .OrderBy(group => group.Key.Year)
            .ThenBy(group => group.Key.Month)
            .Select(group =>
            {
                var periodStart = new DateTime(group.Key.Year, group.Key.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var gross = group.Sum(entry => entry.Row.GrossOrderValue);
                var totalDiscount = group.Sum(entry => entry.Row.TotalDiscountAmount);
                return new AdminFinancialDiscountTrendBucketReadModel
                {
                    Period = $"{group.Key.Year:0000}-{group.Key.Month:00}",
                    PeriodStartUtc = periodStart,
                    GrossOrderValue = gross,
                    TotalDiscountAmount = totalDiscount,
                    DiscountRate = gross > 0m ? Math.Round(totalDiscount / gross * 100m, 2) : 0m,
                    DiscountedOrderCount = group.Count(entry => entry.Row.TotalDiscountAmount > 0m),
                    TotalOrderCount = group.Count()
                };
            })
            .ToList();
    }

    public async Task<IReadOnlyList<AdminFinancialDiscountExceptionReadModel>> GetExceptionsAsync(
        AdminFinancialDiscountQueryReadModel query,
        decimal thresholdRate,
        decimal thresholdAmount,
        CancellationToken cancellationToken = default)
    {
        var rows = await BuildExceptionCandidatesQuery(query, thresholdRate, thresholdAmount)
            .ToListAsync(cancellationToken);

        return rows
            .SelectMany(row => BuildExceptionRows(row, thresholdRate, thresholdAmount))
            .OrderByDescending(row => row.Order.DiscountRate)
            .ThenByDescending(row => row.Order.TotalDiscountAmount)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();
    }

    public async Task<int> CountExceptionsAsync(
        AdminFinancialDiscountQueryReadModel query,
        decimal thresholdRate,
        decimal thresholdAmount,
        CancellationToken cancellationToken = default)
    {
        var rows = await BuildExceptionCandidatesQuery(query, thresholdRate, thresholdAmount)
            .ToListAsync(cancellationToken);
        return rows.Sum(row => BuildExceptionRows(row, thresholdRate, thresholdAmount).Count());
    }

    private IQueryable<AdminFinancialDiscountOrderMetricsReadModel> BuildPagedOrderMetricsQuery(
        AdminFinancialDiscountQueryReadModel query)
    {
        var rows = ApplyOrderMetricsSorting(BuildOrderMetricsQuery(query), query);
        return rows
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize);
    }

    private IQueryable<AdminFinancialDiscountOrderMetricsReadModel> BuildOrderMetricsQuery(
        AdminFinancialDiscountQueryReadModel query)
    {
        var orders = BuildFilteredOrders(query);
        var itemAggregates = _dbContext.OrderItemSet.AsNoTracking()
            .GroupBy(item => item.OrderId)
            .Select(group => new
            {
                OrderId = group.Key,
                GrossOrderValue = group.Sum(item => (decimal?)((item.Quantity ?? 0) * (item.UnitPrice ?? 0m))) ?? 0m,
                ItemDiscountAmount = group.Sum(item => (decimal?)(item.DiscountAmount ?? 0m)) ?? 0m
            });

        var projected = from order in orders
                        join aggregate in itemAggregates on order.OrderId equals aggregate.OrderId into aggregates
                        from aggregate in aggregates.DefaultIfEmpty()
                        join project in _dbContext.ProjectSet.AsNoTracking()
                            on order.ProjectId equals project.ProjectId
                        join customer in _dbContext.AccountSet.AsNoTracking()
                            on order.CustomerId equals customer.AccountId into customers
                        from customer in customers.DefaultIfEmpty()
                        join sales in _dbContext.AccountSet.AsNoTracking()
                            on order.SalesId equals sales.AccountId into salesAccounts
                        from sales in salesAccounts.DefaultIfEmpty()
                        let gross = aggregate == null ? 0m : aggregate.GrossOrderValue
                        let itemDiscount = aggregate == null ? 0m : aggregate.ItemDiscountAmount
                        let orderDiscount = order.AdditionalDiscountAmount ?? 0m
                        let totalDiscount = itemDiscount + orderDiscount
                        let netBeforeVat = gross - totalDiscount
                        select new AdminFinancialDiscountOrderMetricsReadModel
                        {
                            OrderId = order.OrderId,
                            OrderCode = order.OrderCode,
                            OrderStatus = order.Status,
                            ConfirmedAt = order.ConfirmedAt,
                            ProjectId = project.ProjectId,
                            ProjectCode = project.ProjectCode,
                            ProjectName = project.ProjectName,
                            ProjectStatus = project.Status,
                            CustomerId = order.CustomerId,
                            CustomerName = customer != null ? customer.FullName : null,
                            SalesId = order.SalesId,
                            SalesName = sales != null ? sales.FullName : null,
                            GrossOrderValue = gross,
                            ItemDiscountAmount = itemDiscount,
                            OrderAdditionalDiscountAmount = orderDiscount,
                            TotalDiscountAmount = totalDiscount,
                            NetOrderValueBeforeVat = netBeforeVat,
                            VatRate = order.VatRate,
                            VatAmount = order.VatAmount,
                            FinalOrderValue = order.FinalTotalAmount,
                            DiscountRate = gross > 0m ? totalDiscount / gross * 100m : 0m
                        };

        projected = ApplyDiscountMetricFilters(projected, query);
        return projected;
    }

    private IQueryable<AdminFinancialDiscountOrderMetricsReadModel> BuildExceptionCandidatesQuery(
        AdminFinancialDiscountQueryReadModel query,
        decimal thresholdRate,
        decimal thresholdAmount)
    {
        var rows = BuildOrderMetricsQuery(query);
        return rows.Where(row =>
            row.DiscountRate >= thresholdRate || row.TotalDiscountAmount >= thresholdAmount);
    }

    private IQueryable<Domain.Entities.Order> BuildFilteredOrders(AdminFinancialDiscountQueryReadModel query)
    {
        if (query.OrderId.HasValue)
        {
            return _dbContext.OrderSet.AsNoTracking()
                .Where(order => order.OrderId == query.OrderId.Value);
        }

        var orders = _dbContext.OrderSet.AsNoTracking()
            .Where(order =>
                order.Status != OrderStatus.CANCELLED &&
                order.ConfirmedAt.HasValue &&
                order.ConfirmedAt.Value >= query.FromUtc &&
                order.ConfirmedAt.Value < query.ToUtcExclusive);

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
            orders = orders.Where(order => order.SalesId == query.SalesId.Value);
        }

        if (query.ProjectStatus.HasValue)
        {
            orders = orders.Where(order =>
                _dbContext.ProjectSet.Any(project =>
                    project.ProjectId == order.ProjectId &&
                    project.Status == query.ProjectStatus.Value));
        }

        return orders;
    }

    private static IQueryable<AdminFinancialDiscountOrderMetricsReadModel> ApplyDiscountMetricFilters(
        IQueryable<AdminFinancialDiscountOrderMetricsReadModel> rows,
        AdminFinancialDiscountQueryReadModel query)
    {
        if (query.HasDiscount == true)
        {
            rows = rows.Where(row => row.TotalDiscountAmount > 0m);
        }
        else if (query.HasDiscount == false)
        {
            rows = rows.Where(row => row.TotalDiscountAmount <= 0m);
        }

        if (query.MinDiscountRate.HasValue)
        {
            rows = rows.Where(row => row.DiscountRate >= query.MinDiscountRate.Value);
        }

        return rows;
    }

    private static IQueryable<AdminFinancialDiscountOrderMetricsReadModel> ApplyOrderMetricsSorting(
        IQueryable<AdminFinancialDiscountOrderMetricsReadModel> rows,
        AdminFinancialDiscountQueryReadModel query)
    {
        var descending = string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        return (query.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "totaldiscountamount" => descending
                ? rows.OrderByDescending(row => row.TotalDiscountAmount).ThenByDescending(row => row.OrderId)
                : rows.OrderBy(row => row.TotalDiscountAmount).ThenBy(row => row.OrderId),
            "discountrate" => descending
                ? rows.OrderByDescending(row => row.DiscountRate).ThenByDescending(row => row.OrderId)
                : rows.OrderBy(row => row.DiscountRate).ThenBy(row => row.OrderId),
            "finalordervalue" => descending
                ? rows.OrderByDescending(row => row.FinalOrderValue).ThenByDescending(row => row.OrderId)
                : rows.OrderBy(row => row.FinalOrderValue).ThenBy(row => row.OrderId),
            _ => descending
                ? rows.OrderByDescending(row => row.ConfirmedAt).ThenByDescending(row => row.OrderId)
                : rows.OrderBy(row => row.ConfirmedAt).ThenBy(row => row.OrderId)
        };
    }

    private static AdminFinancialDiscountSummaryReadModel AggregateSummary(
        List<AdminFinancialDiscountOrderMetricsReadModel> rows)
    {
        var gross = rows.Sum(row => row.GrossOrderValue);
        var totalDiscount = rows.Sum(row => row.TotalDiscountAmount);
        return new AdminFinancialDiscountSummaryReadModel
        {
            GrossOrderValue = gross,
            ItemDiscountAmount = rows.Sum(row => row.ItemDiscountAmount),
            OrderAdditionalDiscountAmount = rows.Sum(row => row.OrderAdditionalDiscountAmount),
            TotalDiscountAmount = totalDiscount,
            NetOrderValueBeforeVat = rows.Sum(row => row.NetOrderValueBeforeVat),
            VatAmount = rows.Sum(row => row.VatAmount),
            FinalOrderValue = rows.Sum(row => row.FinalOrderValue),
            AverageDiscountRate = gross > 0m ? Math.Round(totalDiscount / gross * 100m, 2) : 0m,
            DiscountedOrderCount = rows.Count(row => row.TotalDiscountAmount > 0m),
            TotalOrderCount = rows.Count
        };
    }

    private static IEnumerable<AdminFinancialDiscountExceptionReadModel> BuildExceptionRows(
        AdminFinancialDiscountOrderMetricsReadModel row,
        decimal thresholdRate,
        decimal thresholdAmount)
    {
        if (row.DiscountRate >= thresholdRate)
        {
            yield return new AdminFinancialDiscountExceptionReadModel
            {
                ExceptionType = AdminFinancialDiscountExceptionTypes.HighDiscountRate,
                Order = row,
                ThresholdRate = thresholdRate,
                ThresholdAmount = thresholdAmount
            };
        }

        if (row.TotalDiscountAmount >= thresholdAmount)
        {
            yield return new AdminFinancialDiscountExceptionReadModel
            {
                ExceptionType = AdminFinancialDiscountExceptionTypes.HighDiscountAmount,
                Order = row,
                ThresholdRate = thresholdRate,
                ThresholdAmount = thresholdAmount
            };
        }
    }
}

internal static class AdminFinancialDiscountExceptionTypes
{
    public const string HighDiscountRate = "HIGH_DISCOUNT_RATE";
    public const string HighDiscountAmount = "HIGH_DISCOUNT_AMOUNT";
}
