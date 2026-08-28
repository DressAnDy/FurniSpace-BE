#nullable enable

using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Financial;
using FurniSpace.Application.DTOs.Financial;
using FurniSpace.Application.Interfaces.Financial;
using FurniSpace.Infrastructure.ReadModels.Financial;
using FurniSpace.Infrastructure.Repositories.IRepository;

namespace FurniSpace.Application.Services.Financial;

public sealed class AdminFinancialDiscountService : IAdminFinancialDiscountService
{
    private const int MaxPageSize = 100;
    private const string GranularityMonth = "MONTH";
    private const string SortAscending = "asc";
    private const string SortDescending = "desc";
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private static readonly HashSet<string> ProjectSortFields =
    [
        "confirmedAt",
        "totalDiscountAmount",
        "discountRate",
        "finalOrderValue"
    ];

    private readonly IFinancialDiscountReadRepository _discounts;

    public AdminFinancialDiscountService(IFinancialDiscountReadRepository discounts)
    {
        _discounts = discounts;
    }

    public async Task<ServiceResult<AdminFinancialDiscountSummaryDto>> GetSummaryAsync(
        AdminFinancialDiscountSummaryQueryDto query,
        CancellationToken cancellationToken = default)
    {
        query ??= new AdminFinancialDiscountSummaryQueryDto();
        if (!TryCreateRequiredReadQuery(query.From, query.To, query.ProjectStatus, query.SalesId, query.CustomerId, null, null, 1, 1, "confirmedAt", SortDescending, out var readQuery, out var errorMessage))
        {
            return ServiceResult<AdminFinancialDiscountSummaryDto>.Failure(
                Error.BadRequest(AdminFinancialDiscountErrorCodes.DateRangeInvalid, errorMessage));
        }

        var currency = FinancialReportingPeriodResolver.NormalizeCurrency(query.Currency);
        if (!IsSupportedCurrency(currency))
        {
            return ServiceResult<AdminFinancialDiscountSummaryDto>.Failure(
                Error.BadRequest(AdminFinancialErrorCodes.CurrencyInvalid, FinancialReportingConstants.CurrencyInvalidMessage));
        }

        var summary = await _discounts.GetSummaryAsync(readQuery, cancellationToken);
        return ServiceResult<AdminFinancialDiscountSummaryDto>.Success(
            new AdminFinancialDiscountSummaryDto
            {
                GrossOrderValue = summary.GrossOrderValue,
                ItemDiscountAmount = summary.ItemDiscountAmount,
                OrderAdditionalDiscountAmount = summary.OrderAdditionalDiscountAmount,
                TotalDiscountAmount = summary.TotalDiscountAmount,
                NetOrderValueBeforeVat = summary.NetOrderValueBeforeVat,
                VatAmount = summary.VatAmount,
                FinalOrderValue = summary.FinalOrderValue,
                AverageDiscountRate = summary.AverageDiscountRate,
                DiscountedOrderCount = summary.DiscountedOrderCount,
                TotalOrderCount = summary.TotalOrderCount,
                PeriodFrom = query.From,
                PeriodTo = query.To,
                Currency = currency
            },
            "Financial discount summary retrieved successfully.");
    }

    public async Task<ServiceResult<AdminFinancialDiscountProjectsDto>> GetProjectsAsync(
        AdminFinancialDiscountProjectsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        query ??= new AdminFinancialDiscountProjectsQueryDto();
        if (!TryCreateRequiredReadQuery(
                query.From,
                query.To,
                null,
                query.SalesId,
                query.CustomerId,
                query.ProjectId,
                query.HasDiscount,
                query.Page,
                query.PageSize,
                query.SortBy,
                query.SortDirection,
                out var readQuery,
                out var errorMessage))
        {
            return ServiceResult<AdminFinancialDiscountProjectsDto>.Failure(
                Error.BadRequest(AdminFinancialDiscountErrorCodes.FilterInvalid, errorMessage));
        }

        readQuery.MinDiscountRate = query.MinDiscountRate;
        var totalItems = await _discounts.CountOrderMetricsAsync(readQuery, cancellationToken);
        var rows = await _discounts.GetOrderMetricsAsync(readQuery, cancellationToken);
        return ServiceResult<AdminFinancialDiscountProjectsDto>.Success(
            new AdminFinancialDiscountProjectsDto
            {
                Items = rows.Select(ToProjectRowDto).ToList(),
                Page = readQuery.Page,
                PageSize = readQuery.PageSize,
                TotalItems = totalItems,
                TotalPages = CalculateTotalPages(totalItems, readQuery.PageSize)
            },
            "Financial discount projects retrieved successfully.");
    }

    public async Task<ServiceResult<AdminFinancialDiscountOrderDetailDto>> GetOrderDetailAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _discounts.GetOrderMetricsByIdAsync(orderId, cancellationToken);
        if (order is null || order.OrderStatus == Domain.Enums.OrderStatus.CANCELLED)
        {
            return ServiceResult<AdminFinancialDiscountOrderDetailDto>.Failure(
                Error.NotFound(AdminFinancialDiscountErrorCodes.OrderNotFound, "Discount order was not found."));
        }

        var items = await _discounts.GetOrderItemsAsync(orderId, cancellationToken);
        return ServiceResult<AdminFinancialDiscountOrderDetailDto>.Success(
            ToOrderDetailDto(order, items),
            "Financial discount order detail retrieved successfully.");
    }

    public async Task<ServiceResult<AdminFinancialDiscountTrendDto>> GetTrendAsync(
        AdminFinancialDiscountTrendQueryDto query,
        CancellationToken cancellationToken = default)
    {
        query ??= new AdminFinancialDiscountTrendQueryDto();
        if (!TryCreateRequiredReadQuery(query.From, query.To, null, query.SalesId, null, null, null, 1, 1, "confirmedAt", SortDescending, out var readQuery, out var errorMessage))
        {
            return ServiceResult<AdminFinancialDiscountTrendDto>.Failure(
                Error.BadRequest(AdminFinancialDiscountErrorCodes.DateRangeInvalid, errorMessage));
        }

        var granularity = NormalizeGranularity(query.Granularity);
        if (granularity != GranularityMonth)
        {
            return ServiceResult<AdminFinancialDiscountTrendDto>.Failure(
                Error.BadRequest(AdminFinancialDiscountErrorCodes.GranularityInvalid, "Discount trend granularity is invalid."));
        }

        var currency = FinancialReportingPeriodResolver.NormalizeCurrency(query.Currency);
        if (!IsSupportedCurrency(currency))
        {
            return ServiceResult<AdminFinancialDiscountTrendDto>.Failure(
                Error.BadRequest(AdminFinancialErrorCodes.CurrencyInvalid, FinancialReportingConstants.CurrencyInvalidMessage));
        }

        var series = await _discounts.GetTrendAsync(readQuery, cancellationToken);
        return ServiceResult<AdminFinancialDiscountTrendDto>.Success(
            new AdminFinancialDiscountTrendDto
            {
                Granularity = granularity,
                Timezone = FinancialReportingConstants.ReportingTimezone,
                Currency = currency,
                Series = series.Select(bucket => new AdminFinancialDiscountTrendBucketDto
                {
                    Period = bucket.Period,
                    PeriodStart = new DateTimeOffset(bucket.PeriodStartUtc, TimeSpan.Zero).ToOffset(VietnamOffset),
                    GrossOrderValue = bucket.GrossOrderValue,
                    TotalDiscountAmount = bucket.TotalDiscountAmount,
                    DiscountRate = bucket.DiscountRate,
                    DiscountedOrderCount = bucket.DiscountedOrderCount,
                    TotalOrderCount = bucket.TotalOrderCount
                }).ToList()
            },
            "Financial discount trend retrieved successfully.");
    }

    public async Task<ServiceResult<AdminFinancialDiscountExceptionsDto>> GetExceptionsAsync(
        AdminFinancialDiscountExceptionsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        query ??= new AdminFinancialDiscountExceptionsQueryDto();
        if (!TryCreateRequiredReadQuery(query.From, query.To, null, query.SalesId, null, null, null, query.Page, query.PageSize, "confirmedAt", SortDescending, out var readQuery, out var errorMessage))
        {
            return ServiceResult<AdminFinancialDiscountExceptionsDto>.Failure(
                Error.BadRequest(AdminFinancialDiscountErrorCodes.FilterInvalid, errorMessage));
        }

        var thresholdRate = query.ThresholdRate ?? 20m;
        var thresholdAmount = query.ThresholdAmount ?? 1_000_000m;
        if (thresholdRate < 0m || thresholdAmount < 0m)
        {
            return ServiceResult<AdminFinancialDiscountExceptionsDto>.Failure(
                Error.BadRequest(AdminFinancialDiscountErrorCodes.FilterInvalid, "Discount exception thresholds are invalid."));
        }

        var totalItems = await _discounts.CountExceptionsAsync(readQuery, thresholdRate, thresholdAmount, cancellationToken);
        var rows = await _discounts.GetExceptionsAsync(readQuery, thresholdRate, thresholdAmount, cancellationToken);
        return ServiceResult<AdminFinancialDiscountExceptionsDto>.Success(
            new AdminFinancialDiscountExceptionsDto
            {
                Items = rows.Select(ToExceptionRowDto).ToList(),
                Page = readQuery.Page,
                PageSize = readQuery.PageSize,
                TotalItems = totalItems,
                TotalPages = CalculateTotalPages(totalItems, readQuery.PageSize)
            },
            "Financial discount exceptions retrieved successfully.");
    }

    private static AdminFinancialDiscountProjectRowDto ToProjectRowDto(AdminFinancialDiscountOrderMetricsReadModel row) =>
        new()
        {
            ProjectId = row.ProjectId,
            ProjectCode = row.ProjectCode,
            ProjectName = row.ProjectName,
            ProjectStatus = row.ProjectStatus,
            CustomerId = row.CustomerId,
            CustomerName = row.CustomerName,
            SalesId = row.SalesId,
            SalesName = row.SalesName,
            OrderId = row.OrderId,
            OrderCode = row.OrderCode,
            OrderStatus = row.OrderStatus,
            ConfirmedAt = ToOffset(row.ConfirmedAt),
            GrossOrderValue = row.GrossOrderValue,
            ItemDiscountAmount = row.ItemDiscountAmount,
            OrderAdditionalDiscountAmount = row.OrderAdditionalDiscountAmount,
            TotalDiscountAmount = row.TotalDiscountAmount,
            NetOrderValueBeforeVat = row.NetOrderValueBeforeVat,
            VatAmount = row.VatAmount,
            FinalOrderValue = row.FinalOrderValue,
            DiscountRate = row.DiscountRate
        };

    private static AdminFinancialDiscountOrderDetailDto ToOrderDetailDto(
        AdminFinancialDiscountOrderMetricsReadModel row,
        IReadOnlyList<AdminFinancialDiscountOrderItemReadModel> items) =>
        new()
        {
            OrderId = row.OrderId,
            OrderCode = row.OrderCode,
            OrderStatus = row.OrderStatus,
            ConfirmedAt = ToOffset(row.ConfirmedAt),
            ProjectId = row.ProjectId,
            ProjectCode = row.ProjectCode,
            ProjectName = row.ProjectName,
            CustomerId = row.CustomerId,
            CustomerName = row.CustomerName,
            GrossOrderValue = row.GrossOrderValue,
            ItemDiscountAmount = row.ItemDiscountAmount,
            OrderAdditionalDiscountAmount = row.OrderAdditionalDiscountAmount,
            TotalDiscountAmount = row.TotalDiscountAmount,
            NetOrderValueBeforeVat = row.NetOrderValueBeforeVat,
            VatRate = row.VatRate,
            VatAmount = row.VatAmount,
            FinalOrderValue = row.FinalOrderValue,
            DiscountRate = row.DiscountRate,
            Items = items.Select(item => new AdminFinancialDiscountOrderItemDto
            {
                OrderItemId = item.OrderItemId,
                ProductName = item.ProductName,
                ProductVersionName = item.ProductVersionName,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                LineGrossAmount = item.LineGrossAmount,
                DiscountAmount = item.DiscountAmount,
                SubtotalAmount = item.SubtotalAmount
            }).ToList()
        };

    private static AdminFinancialDiscountExceptionRowDto ToExceptionRowDto(AdminFinancialDiscountExceptionReadModel row) =>
        new()
        {
            ExceptionType = row.ExceptionType,
            OrderId = row.Order.OrderId,
            OrderCode = row.Order.OrderCode,
            ProjectId = row.Order.ProjectId,
            ProjectCode = row.Order.ProjectCode,
            ProjectName = row.Order.ProjectName,
            SalesId = row.Order.SalesId,
            SalesName = row.Order.SalesName,
            ConfirmedAt = ToOffset(row.Order.ConfirmedAt),
            GrossOrderValue = row.Order.GrossOrderValue,
            TotalDiscountAmount = row.Order.TotalDiscountAmount,
            DiscountRate = row.Order.DiscountRate,
            FinalOrderValue = row.Order.FinalOrderValue,
            ThresholdRate = row.ThresholdRate,
            ThresholdAmount = row.ThresholdAmount
        };

    private static bool TryCreateRequiredReadQuery(
        DateTimeOffset? from,
        DateTimeOffset? to,
        Domain.Enums.ProjectStatus? projectStatus,
        Guid? salesId,
        Guid? customerId,
        Guid? projectId,
        bool? hasDiscount,
        int page,
        int pageSize,
        string? sortBy,
        string? sortDirection,
        out AdminFinancialDiscountQueryReadModel readQuery,
        out string errorMessage)
    {
        readQuery = new AdminFinancialDiscountQueryReadModel();
        if (!TryResolveFinancialRange(from, to, out var fromUtc, out var toUtcExclusive))
        {
            errorMessage = "Discount date range is invalid.";
            return false;
        }

        if (page <= 0 || pageSize <= 0 || pageSize > MaxPageSize)
        {
            errorMessage = "Discount pagination is invalid.";
            return false;
        }

        var normalizedSortBy = string.IsNullOrWhiteSpace(sortBy) ? "confirmedAt" : sortBy.Trim();
        if (!ProjectSortFields.Contains(normalizedSortBy, StringComparer.OrdinalIgnoreCase))
        {
            errorMessage = "Discount sort field is invalid.";
            return false;
        }

        var normalizedSortDirection = string.IsNullOrWhiteSpace(sortDirection) ? SortDescending : sortDirection.Trim().ToLowerInvariant();
        if (normalizedSortDirection is not SortAscending and not SortDescending)
        {
            errorMessage = "Discount sort direction is invalid.";
            return false;
        }

        readQuery = new AdminFinancialDiscountQueryReadModel
        {
            FromUtc = fromUtc!.Value,
            ToUtcExclusive = toUtcExclusive!.Value,
            ProjectStatus = projectStatus,
            SalesId = salesId,
            CustomerId = customerId,
            ProjectId = projectId,
            HasDiscount = hasDiscount,
            Page = page,
            PageSize = pageSize,
            SortBy = normalizedSortBy,
            SortDirection = normalizedSortDirection
        };
        errorMessage = string.Empty;
        return true;
    }

    private static bool TryResolveFinancialRange(
        DateTimeOffset? from,
        DateTimeOffset? to,
        out DateTime? fromUtc,
        out DateTime? toUtcExclusive)
    {
        fromUtc = null;
        toUtcExclusive = null;
        if (!from.HasValue || !to.HasValue)
        {
            return false;
        }

        if (from.Value > to.Value)
        {
            return false;
        }

        fromUtc = from.Value.UtcDateTime;
        toUtcExclusive = IsStartOfDay(to.Value)
            ? new DateTimeOffset(to.Value.Date.AddDays(1), to.Value.Offset).UtcDateTime
            : to.Value.AddTicks(1).UtcDateTime;
        return true;
    }

    private static string NormalizeGranularity(string? granularity) =>
        string.IsNullOrWhiteSpace(granularity) ? GranularityMonth : granularity.Trim().ToUpperInvariant();

    private static bool IsSupportedCurrency(string currency) =>
        string.Equals(currency, FinancialReportingConstants.DefaultCurrency, StringComparison.Ordinal);

    private static DateTimeOffset? ToOffset(DateTime? value) =>
        value.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)).ToOffset(VietnamOffset)
            : null;

    private static int CalculateTotalPages(int totalItems, int pageSize) =>
        totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

    private static bool IsStartOfDay(DateTimeOffset value) => value.TimeOfDay == TimeSpan.Zero;
}
