using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Financial;
using FurniSpace.Application.DTOs.Financial;
using FurniSpace.Application.Interfaces.Financial;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Financial;
using FurniSpace.Infrastructure.Repositories.IRepository;

namespace FurniSpace.Application.Services.Financial;

public sealed class AdminFinancialService : IAdminFinancialService
{
    private const int MaxPageSize = 100;
    private const string GranularityMonth = "MONTH";
    private const string SortAscending = "asc";
    private const string SortDescending = "desc";
    private const string CreatedAtSortField = "createdAt";
    private const string ExceptionPaymentExpired = "PAYMENT_EXPIRED";
    private const string ExceptionPaymentRepeatedFailure = "PAYMENT_REPEATED_FAILURE";
    private const string ExceptionFinalPaymentNotCreated = "FINAL_PAYMENT_NOT_CREATED";
    private const string ExceptionDeliveredWithReceivable = "DELIVERED_WITH_RECEIVABLE";
    private const string ExceptionPaymentPendingTooLong = "PAYMENT_PENDING_TOO_LONG";
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private static readonly HashSet<string> ReceivableSortFields =
    [
        "confirmedAt",
        "projectCode",
        "projectName",
        "orderCode",
        "orderStatus",
        "finalTotalAmount",
        "remainingAmount"
    ];
    private static readonly HashSet<string> ProjectSortFields =
    [
        CreatedAtSortField,
        "projectCode",
        "projectName",
        "projectStatus",
        "orderFinalTotal",
        "orderRemainingAmount",
        "totalProjectCashCollected",
        "lastPaidAt"
    ];
    private static readonly HashSet<string> PaymentSortFields =
    [
        CreatedAtSortField,
        "paidAt",
        "expiredAt",
        "amount",
        "paymentCode",
        "status"
    ];
    private static readonly HashSet<string> ExceptionTypes =
    [
        ExceptionPaymentExpired,
        ExceptionPaymentRepeatedFailure,
        ExceptionFinalPaymentNotCreated,
        ExceptionDeliveredWithReceivable,
        ExceptionPaymentPendingTooLong
    ];
    private static readonly HashSet<string> SupportedSummaryMetrics = new(StringComparer.Ordinal)
    {
        AdminFinancialSummaryMetrics.Collected,
        AdminFinancialSummaryMetrics.Outstanding,
        AdminFinancialSummaryMetrics.ContractedReceivable,
        AdminFinancialSummaryMetrics.OrderValue,
        AdminFinancialSummaryMetrics.FailedTransactions,
        AdminFinancialSummaryMetrics.ActivePayments
    };

    private readonly IFinancialReadRepository _financial;

    public AdminFinancialService(IFinancialReadRepository financial)
    {
        _financial = financial;
    }

    public async Task<ServiceResult<AdminFinancialSummaryDto>> GetSummaryAsync(
        AdminFinancialSummaryQueryDto query,
        CancellationToken cancellationToken = default)
    {
        query ??= new AdminFinancialSummaryQueryDto();
        var currency = FinancialReportingPeriodResolver.NormalizeCurrency(query.Currency);
        if (!string.Equals(currency, FinancialReportingConstants.DefaultCurrency, StringComparison.Ordinal))
        {
            return ServiceResult<AdminFinancialSummaryDto>.Failure(
                Error.BadRequest(
                    AdminFinancialErrorCodes.CurrencyInvalid,
                    FinancialReportingConstants.CurrencyInvalidMessage));
        }

        if (!FinancialReportingPeriodResolver.TryResolve(
                query,
                DateTimeOffset.UtcNow,
                out var period,
                out var errorCode,
                out var errorMessage))
        {
            return ServiceResult<AdminFinancialSummaryDto>.Failure(
                Error.BadRequest(errorCode, errorMessage));
        }

        var summary = await _financial.GetAdminSummaryAsync(
            period.FromUtc,
            period.ToUtcExclusive,
            DateTime.UtcNow,
            currency,
            FinancialReportingConstants.CanonicalCollectedPaymentTypes,
            cancellationToken);

        return ServiceResult<AdminFinancialSummaryDto>.Success(
            new AdminFinancialSummaryDto
            {
                Period = new AdminFinancialPeriodDto
                {
                    Type = period.Type,
                    From = period.From,
                    To = period.To,
                    Timezone = period.Timezone
                },
                Currency = currency,
                CollectedAmount = summary.CollectedAmount,
                OutstandingPaymentAmount = summary.OutstandingPaymentAmount,
                ContractedReceivableAmount = summary.ContractedReceivableAmount,
                OrderCommercialValue = summary.OrderCommercialValue,
                FailedTransactionCount = summary.FailedTransactionCount,
                ActivePaymentCount = summary.ActivePaymentCount
            },
            "Admin financial summary retrieved successfully.");
    }

    public async Task<ServiceResult<AdminFinancialReceivablesDto>> GetReceivablesAsync(
        AdminFinancialReceivablesQueryDto query,
        CancellationToken cancellationToken = default)
    {
        query ??= new AdminFinancialReceivablesQueryDto();
        if (!TryCreateReceivablesReadQuery(query, out var readQuery, out var errorMessage))
        {
            return ServiceResult<AdminFinancialReceivablesDto>.Failure(
                Error.BadRequest(AdminFinancialErrorCodes.ReceivableFilterInvalid, errorMessage));
        }

        var utcNow = DateTime.UtcNow;
        var summary = await _financial.GetReceivablesSummaryAsync(readQuery, utcNow, cancellationToken);
        var totalItems = await _financial.CountReceivableItemsAsync(readQuery, utcNow, cancellationToken);
        var items = await _financial.GetReceivableItemsAsync(readQuery, utcNow, cancellationToken);

        return ServiceResult<AdminFinancialReceivablesDto>.Success(
            new AdminFinancialReceivablesDto
            {
                OutstandingPaymentAmount = summary.OutstandingPaymentAmount,
                OutstandingPaymentCount = summary.OutstandingPaymentCount,
                ContractedReceivableAmount = summary.ContractedReceivableAmount,
                OrdersWithReceivableCount = summary.OrdersWithReceivableCount,
                Items = items.Select(ToReceivableItemDto).ToList(),
                Page = readQuery.Page,
                PageSize = readQuery.PageSize,
                TotalItems = totalItems,
                TotalPages = CalculateTotalPages(totalItems, readQuery.PageSize)
            },
            "Financial receivables retrieved successfully.");
    }

    public async Task<ServiceResult<AdminFinancialPaymentBreakdownDto>> GetPaymentBreakdownAsync(
        AdminFinancialPaymentBreakdownQueryDto query,
        CancellationToken cancellationToken = default)
    {
        query ??= new AdminFinancialPaymentBreakdownQueryDto();
        if (!TryResolveFinancialRange(query.From, query.To, out var fromUtc, out var toUtcExclusive))
        {
            return ServiceResult<AdminFinancialPaymentBreakdownDto>.Failure(
                Error.BadRequest(AdminFinancialErrorCodes.DateRangeInvalid, "Financial date range is invalid."));
        }

        var currency = FinancialReportingPeriodResolver.NormalizeCurrency(query.Currency);
        if (!IsSupportedCurrency(currency))
        {
            return ServiceResult<AdminFinancialPaymentBreakdownDto>.Failure(
                Error.BadRequest(AdminFinancialErrorCodes.CurrencyInvalid, FinancialReportingConstants.CurrencyInvalidMessage));
        }

        var rows = await _financial.GetPaymentBreakdownAsync(
            fromUtc!.Value,
            toUtcExclusive!.Value,
            DateTime.UtcNow,
            currency,
            FinancialReportingConstants.CanonicalCollectedPaymentTypes,
            cancellationToken);

        return ServiceResult<AdminFinancialPaymentBreakdownDto>.Success(
            new AdminFinancialPaymentBreakdownDto
            {
                Currency = currency,
                Items = rows.Select(ToBreakdownItemDto).ToList()
            },
            "Payment breakdown retrieved successfully.");
    }

    public async Task<ServiceResult<AdminFinancialCollectionTrendDto>> GetCollectionTrendAsync(
        AdminFinancialCollectionTrendQueryDto query,
        CancellationToken cancellationToken = default)
    {
        query ??= new AdminFinancialCollectionTrendQueryDto();
        if (!TryResolveFinancialRange(query.From, query.To, out var fromUtc, out var toUtcExclusive))
        {
            return ServiceResult<AdminFinancialCollectionTrendDto>.Failure(
                Error.BadRequest(AdminFinancialErrorCodes.DateRangeInvalid, "Financial date range is invalid."));
        }

        var granularity = NormalizeGranularity(query.Granularity);
        if (granularity != GranularityMonth)
        {
            return ServiceResult<AdminFinancialCollectionTrendDto>.Failure(
                Error.BadRequest(AdminFinancialErrorCodes.GranularityInvalid, "Financial granularity is invalid."));
        }

        var currency = FinancialReportingPeriodResolver.NormalizeCurrency(query.Currency);
        if (!IsSupportedCurrency(currency))
        {
            return ServiceResult<AdminFinancialCollectionTrendDto>.Failure(
                Error.BadRequest(AdminFinancialErrorCodes.CurrencyInvalid, FinancialReportingConstants.CurrencyInvalidMessage));
        }

        var series = await BuildMonthlyTrendSeriesAsync(
            query.From!.Value.ToOffset(VietnamOffset),
            query.To!.Value.ToOffset(VietnamOffset),
            fromUtc!.Value,
            toUtcExclusive!.Value,
            currency,
            cancellationToken);

        return ServiceResult<AdminFinancialCollectionTrendDto>.Success(
            new AdminFinancialCollectionTrendDto
            {
                Granularity = granularity,
                Timezone = FinancialReportingConstants.ReportingTimezone,
                Currency = currency,
                Series = series
            },
            "Collection trend retrieved successfully.");
    }

    public async Task<ServiceResult<AdminFinancialProjectsDto>> GetProjectsAsync(
        AdminFinancialProjectsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        query ??= new AdminFinancialProjectsQueryDto();
        if (!TryCreateProjectsReadQuery(query, out var readQuery, out var errorMessage))
        {
            return ServiceResult<AdminFinancialProjectsDto>.Failure(
                Error.BadRequest(AdminFinancialErrorCodes.ProjectFilterInvalid, errorMessage));
        }

        var utcNow = DateTime.UtcNow;
        var totalItems = await _financial.CountProjectFinancialRowsAsync(readQuery, utcNow, cancellationToken);
        var rows = await _financial.GetProjectFinancialRowsAsync(
            readQuery,
            utcNow,
            FinancialReportingConstants.CanonicalCollectedPaymentTypes,
            cancellationToken);

        return ServiceResult<AdminFinancialProjectsDto>.Success(
            new AdminFinancialProjectsDto
            {
                Items = rows.Select(ToProjectRowDto).ToList(),
                Page = readQuery.Page,
                PageSize = readQuery.PageSize,
                TotalItems = totalItems,
                TotalPages = CalculateTotalPages(totalItems, readQuery.PageSize)
            },
            "Project financial overview retrieved successfully.");
    }

    public async Task<ServiceResult<AdminFinancialProjectRowDto>> GetProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var row = await _financial.GetProjectFinancialRowAsync(
            projectId,
            DateTime.UtcNow,
            FinancialReportingConstants.CanonicalCollectedPaymentTypes,
            cancellationToken);
        return row is null
            ? ServiceResult<AdminFinancialProjectRowDto>.Failure(
                Error.NotFound(AdminFinancialErrorCodes.ProjectNotFound, "Project not found."))
            : ServiceResult<AdminFinancialProjectRowDto>.Success(
                ToProjectRowDto(row),
                "Project financial detail retrieved successfully.");
    }

    public async Task<ServiceResult<AdminFinancialPaymentsDto>> GetPaymentsAsync(
        AdminFinancialPaymentsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        query ??= new AdminFinancialPaymentsQueryDto();
        if (!TryCreatePaymentsReadQuery(query, out var readQuery, out var errorMessage))
        {
            return ServiceResult<AdminFinancialPaymentsDto>.Failure(
                Error.BadRequest(AdminFinancialErrorCodes.PaymentFilterInvalid, errorMessage));
        }

        var totalItems = await _financial.CountOperationalPaymentsAsync(readQuery, cancellationToken);
        var rows = await _financial.GetOperationalPaymentsAsync(readQuery, cancellationToken);
        return ServiceResult<AdminFinancialPaymentsDto>.Success(
            new AdminFinancialPaymentsDto
            {
                Items = rows.Select(ToPaymentRowDto).ToList(),
                Page = readQuery.Page,
                PageSize = readQuery.PageSize,
                TotalItems = totalItems,
                TotalPages = CalculateTotalPages(totalItems, readQuery.PageSize)
            },
            "Financial payments retrieved successfully.");
    }

    public async Task<ServiceResult<AdminFinancialExceptionsDto>> GetExceptionsAsync(
        AdminFinancialExceptionsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        query ??= new AdminFinancialExceptionsQueryDto();
        if (!TryCreateExceptionsReadQuery(query, out var readQuery, out var errorCode, out var errorMessage))
        {
            return ServiceResult<AdminFinancialExceptionsDto>.Failure(
                Error.BadRequest(errorCode, errorMessage));
        }

        var utcNow = DateTime.UtcNow;
        var totalItems = await _financial.CountFinancialExceptionsAsync(readQuery, utcNow, cancellationToken);
        var rows = await _financial.GetFinancialExceptionsAsync(readQuery, utcNow, cancellationToken);
        return ServiceResult<AdminFinancialExceptionsDto>.Success(
            new AdminFinancialExceptionsDto
            {
                Items = rows.Select(ToExceptionRowDto).ToList(),
                Page = readQuery.Page,
                PageSize = readQuery.PageSize,
                TotalItems = totalItems,
                TotalPages = CalculateTotalPages(totalItems, readQuery.PageSize)
            },
            "Financial exceptions retrieved successfully.");
    }

    public async Task<ServiceResult<AdminFinancialSummaryDrilldownDto>> GetSummaryDrilldownAsync(
        string metric,
        AdminFinancialSummaryDrilldownQueryDto query,
        CancellationToken cancellationToken = default)
    {
        query ??= new AdminFinancialSummaryDrilldownQueryDto();
        var normalizedMetric = NormalizeOptionalUpper(metric);
        if (string.IsNullOrWhiteSpace(normalizedMetric) || !SupportedSummaryMetrics.Contains(normalizedMetric))
        {
            return ServiceResult<AdminFinancialSummaryDrilldownDto>.Failure(
                Error.BadRequest(
                    AdminFinancialErrorCodes.MetricInvalid,
                    "Financial summary metric is invalid."));
        }

        var groupBy = NormalizeOptionalUpper(query.GroupBy);
        if (!IsSupportedDrilldownGroupBy(normalizedMetric, groupBy))
        {
            return ServiceResult<AdminFinancialSummaryDrilldownDto>.Failure(
                Error.BadRequest(
                    AdminFinancialErrorCodes.GroupByInvalid,
                    "Financial summary drilldown groupBy is invalid."));
        }

        var currency = FinancialReportingPeriodResolver.NormalizeCurrency(query.Currency);
        if (!IsSupportedCurrency(currency))
        {
            return ServiceResult<AdminFinancialSummaryDrilldownDto>.Failure(
                Error.BadRequest(AdminFinancialErrorCodes.CurrencyInvalid, FinancialReportingConstants.CurrencyInvalidMessage));
        }

        if (!FinancialReportingPeriodResolver.TryResolve(
                new AdminFinancialSummaryQueryDto
                {
                    Period = FinancialReportingConstants.PeriodCustom,
                    From = query.From,
                    To = query.To,
                    Currency = currency
                },
                DateTimeOffset.UtcNow,
                out var period,
                out var errorCode,
                out var errorMessage))
        {
            return ServiceResult<AdminFinancialSummaryDrilldownDto>.Failure(
                Error.BadRequest(errorCode, errorMessage));
        }

        if (!IsValidPage(query.Page, query.PageSize <= 0 ? 10 : query.PageSize))
        {
            return ServiceResult<AdminFinancialSummaryDrilldownDto>.Failure(
                Error.BadRequest(
                    AdminFinancialErrorCodes.PaymentFilterInvalid,
                    "Page must be greater than zero and page size must be between 1 and 100."));
        }

        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 10 : query.PageSize;
        var sortBy = string.IsNullOrWhiteSpace(query.SortBy)
            ? (groupBy == AdminFinancialDrilldownGroupBy.Project ? "totalCollectedAmount" : "occurredAt")
            : query.SortBy.Trim();
        var sortDirection = NormalizeSortDirection(query.SortDirection);

        var readQuery = new AdminFinancialSummaryDrilldownQueryReadModel
        {
            Metric = normalizedMetric,
            ProjectId = query.ProjectId,
            PaymentType = query.PaymentType,
            Status = NormalizeOptionalUpper(query.Status),
            Provider = query.Provider,
            GroupBy = groupBy,
            Page = page,
            PageSize = pageSize,
            SortBy = sortBy,
            SortDirection = sortDirection
        };

        var drilldown = await _financial.GetSummaryDrilldownAsync(
            readQuery,
            period.FromUtc,
            period.ToUtcExclusive,
            DateTime.UtcNow,
            currency,
            FinancialReportingConstants.CanonicalCollectedPaymentTypes,
            cancellationToken);

        return ServiceResult<AdminFinancialSummaryDrilldownDto>.Success(
            MapDrilldownDto(drilldown, currency, period, page, pageSize),
            "Financial summary drilldown retrieved successfully.");
    }

    private static AdminFinancialSummaryDrilldownDto MapDrilldownDto(
        AdminFinancialSummaryDrilldownReadModel drilldown,
        string currency,
        FinancialReportingPeriod period,
        int page,
        int pageSize)
    {
        var totalAmount = drilldown.TotalAmount;
        return new AdminFinancialSummaryDrilldownDto
        {
            Metric = drilldown.Metric,
            TotalAmount = totalAmount,
            TotalCount = drilldown.TotalCount,
            Currency = currency,
            Period = new AdminFinancialPeriodDto
            {
                Type = period.Type,
                From = period.From,
                To = period.To,
                Timezone = period.Timezone
            },
            Breakdowns = drilldown.Breakdowns.Select(breakdown => new AdminFinancialDrilldownBreakdownDto
            {
                Dimension = breakdown.Dimension,
                Items = breakdown.Items.Select(item => new AdminFinancialDrilldownBreakdownItemDto
                {
                    Key = item.Key,
                    Label = item.Label,
                    Amount = item.Amount,
                    Count = item.Count,
                    Percentage = totalAmount <= 0m
                        ? 0m
                        : Math.Round(item.Amount * 100m / totalAmount, 2, MidpointRounding.AwayFromZero)
                }).ToList()
            }).ToList(),
            Items = drilldown.Items.Select(ToDrilldownItemDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = drilldown.TotalItems,
            TotalPages = CalculateTotalPages(drilldown.TotalItems, pageSize)
        };
    }

    private static AdminFinancialDrilldownItemDto ToDrilldownItemDto(AdminFinancialDrilldownItemReadModel item)
    {
        return new AdminFinancialDrilldownItemDto
        {
            ResourceType = item.ResourceType,
            ProjectId = item.ProjectId,
            ProjectCode = item.ProjectCode,
            ProjectName = item.ProjectName,
            OrderId = item.OrderId,
            OrderCode = item.OrderCode,
            OrderStatus = item.OrderStatus,
            PaymentId = item.PaymentId,
            PaymentCode = item.PaymentCode,
            TransactionId = item.TransactionId,
            PaymentType = item.PaymentType,
            Status = item.Status,
            Provider = item.Provider,
            Amount = item.Amount,
            PaidAmount = item.PaidAmount,
            RemainingAmount = item.RemainingAmount,
            OccurredAt = ToOffset(item.OccurredAt),
            ExpiredAt = ToOffset(item.ExpiredAt),
            FailureReason = item.FailureReason,
            AgeDays = item.AgeDays,
            ProjectStartFeeAmount = item.ProjectStartFeeAmount,
            DepositAmount = item.DepositAmount,
            RemainingPaymentAmount = item.RemainingPaymentAmount,
            FullPaymentAmount = item.FullPaymentAmount,
            TotalCollectedAmount = item.TotalCollectedAmount,
            PaymentCount = item.PaymentCount,
            LastPaidAt = ToOffset(item.LastPaidAt)
        };
    }

    private static bool IsSupportedDrilldownGroupBy(string metric, string? groupBy)
    {
        if (string.IsNullOrWhiteSpace(groupBy))
        {
            return true;
        }

        return metric == AdminFinancialSummaryMetrics.Collected
            && string.Equals(groupBy, AdminFinancialDrilldownGroupBy.Project, StringComparison.Ordinal);
    }

    private static DateTimeOffset? ToOffset(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var utc = DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
        return new DateTimeOffset(utc).ToOffset(VietnamOffset);
    }

    private async Task<List<AdminFinancialCollectionTrendBucketDto>> BuildMonthlyTrendSeriesAsync(
        DateTimeOffset fromLocal,
        DateTimeOffset toLocal,
        DateTime fromUtc,
        DateTime toUtcExclusive,
        string currency,
        CancellationToken cancellationToken)
    {
        var buckets = new List<AdminFinancialCollectionTrendBucketDto>();
        var currentMonth = new DateTimeOffset(fromLocal.Year, fromLocal.Month, 1, 0, 0, 0, VietnamOffset);
        var lastMonth = new DateTimeOffset(toLocal.Year, toLocal.Month, 1, 0, 0, 0, VietnamOffset);
        while (currentMonth <= lastMonth)
        {
            var nextMonth = currentMonth.AddMonths(1);
            var bucketFromUtc = MaxUtc(fromUtc, currentMonth.UtcDateTime);
            var bucketToUtcExclusive = MinUtc(toUtcExclusive, nextMonth.UtcDateTime);
            var rows = await _financial.GetCollectedAmountsByPaymentTypeAsync(
                bucketFromUtc,
                bucketToUtcExclusive,
                currency,
                FinancialReportingConstants.CanonicalCollectedPaymentTypes,
                cancellationToken);
            buckets.Add(CreateTrendBucket(currentMonth, rows));
            currentMonth = nextMonth;
        }

        return buckets;
    }

    private static AdminFinancialReceivableItemDto ToReceivableItemDto(AdminFinancialReceivableItemReadModel item)
    {
        return new AdminFinancialReceivableItemDto
        {
            ProjectId = item.ProjectId,
            ProjectCode = item.ProjectCode,
            ProjectName = item.ProjectName,
            OrderId = item.OrderId,
            OrderCode = item.OrderCode,
            OrderStatus = item.OrderStatus,
            FinalTotalAmount = item.FinalTotalAmount,
            PaidAmount = item.PaidAmount,
            RemainingAmount = item.RemainingAmount,
            ActivePaymentId = item.ActivePaymentId,
            ActivePaymentType = item.ActivePaymentType,
            ActivePaymentAmount = item.ActivePaymentAmount,
            ActivePaymentStatus = item.ActivePaymentStatus,
            IsPaymentCreated = item.ActivePaymentId.HasValue
        };
    }

    private static AdminFinancialProjectRowDto ToProjectRowDto(AdminFinancialProjectRowReadModel item)
    {
        return new AdminFinancialProjectRowDto
        {
            ProjectId = item.ProjectId,
            ProjectCode = item.ProjectCode,
            ProjectName = item.ProjectName,
            ProjectStatus = item.ProjectStatus,
            CustomerId = item.CustomerId,
            CustomerName = item.CustomerName,
            AssignedSalesId = item.AssignedSalesId,
            AssignedSalesName = item.AssignedSalesName,
            ProjectStartFeeAmount = item.ProjectStartFeeAmount,
            ProjectStartFeeStatus = item.ProjectStartFeeStatus,
            ProjectStartFeePaidAt = item.ProjectStartFeePaidAt,
            OrderId = item.OrderId,
            OrderCode = item.OrderCode,
            OrderStatus = item.OrderStatus,
            OrderOriginalTotal = item.OrderOriginalTotal,
            OrderAdjustmentAmount = item.OrderAdjustmentAmount,
            OrderAdditionalDiscount = item.OrderAdditionalDiscount,
            OrderFinalTotal = item.OrderFinalTotal,
            OrderPaidAmount = item.OrderPaidAmount,
            OrderRemainingAmount = item.OrderRemainingAmount,
            ActivePaymentId = item.ActivePaymentId,
            ActivePaymentType = item.ActivePaymentType,
            ActivePaymentAmount = item.ActivePaymentAmount,
            ActivePaymentStatus = item.ActivePaymentStatus,
            TotalProjectCashCollected = item.TotalProjectCashCollected,
            LastPaidAt = item.LastPaidAt
        };
    }

    private static AdminFinancialPaymentRowDto ToPaymentRowDto(AdminFinancialPaymentRowReadModel item)
    {
        return new AdminFinancialPaymentRowDto
        {
            PaymentId = item.PaymentId,
            PaymentCode = item.PaymentCode,
            ProjectId = item.ProjectId,
            ProjectCode = item.ProjectCode,
            OrderId = item.OrderId,
            OrderCode = item.OrderCode,
            CustomerId = item.CustomerId,
            CustomerName = item.CustomerName,
            PaymentType = item.PaymentType,
            Amount = item.Amount,
            Currency = item.Currency,
            Status = item.Status,
            CreatedAt = item.CreatedAt,
            ExpiredAt = item.ExpiredAt,
            PaidAt = item.PaidAt,
            LastProvider = item.LastProvider,
            AttemptCount = item.AttemptCount,
            FailedAttemptCount = item.FailedAttemptCount,
            LastTransactionStatus = item.LastTransactionStatus,
            LastFailureReason = item.LastFailureReason,
            LastAttemptAt = item.LastAttemptAt
        };
    }

    private static AdminFinancialExceptionRowDto ToExceptionRowDto(AdminFinancialExceptionRowReadModel item)
    {
        return new AdminFinancialExceptionRowDto
        {
            ExceptionType = item.ExceptionType,
            Severity = item.Severity,
            ProjectId = item.ProjectId,
            OrderId = item.OrderId,
            PaymentId = item.PaymentId,
            Title = item.Title,
            Reason = item.Reason,
            Amount = item.Amount,
            Age = item.Age,
            OccurredAt = item.OccurredAt,
            RecommendedAction = item.RecommendedAction,
            TargetResourceType = item.TargetResourceType,
            TargetResourceId = item.TargetResourceId
        };
    }

    private static AdminFinancialPaymentBreakdownItemDto ToBreakdownItemDto(
        AdminFinancialPaymentTypeBreakdownReadModel item)
    {
        return new AdminFinancialPaymentBreakdownItemDto
        {
            PaymentType = item.PaymentType,
            CollectedAmount = item.CollectedAmount,
            PaidCount = item.PaidCount,
            OutstandingAmount = item.OutstandingAmount,
            OutstandingCount = item.OutstandingCount,
            ExpiredCount = item.ExpiredCount
        };
    }

    private static AdminFinancialCollectionTrendBucketDto CreateTrendBucket(
        DateTimeOffset month,
        IReadOnlyList<AdminFinancialPaymentTypeAmountReadModel> rows)
    {
        var projectStartFee = GetAmount(rows, PaymentType.PROJECT_START_FEE);
        var deposit = GetAmount(rows, PaymentType.DEPOSIT);
        var remainingPayment = GetAmount(rows, PaymentType.REMAINING_PAYMENT);
        return new AdminFinancialCollectionTrendBucketDto
        {
            Period = month.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture),
            ProjectStartFee = projectStartFee,
            Deposit = deposit,
            RemainingPayment = remainingPayment,
            Total = projectStartFee + deposit + remainingPayment
        };
    }

    private static bool TryCreateReceivablesReadQuery(
        AdminFinancialReceivablesQueryDto query,
        out AdminFinancialReceivablesQueryReadModel readQuery,
        out string errorMessage)
    {
        readQuery = new AdminFinancialReceivablesQueryReadModel();
        if (query.Page <= 0 || query.PageSize <= 0 || query.PageSize > MaxPageSize)
        {
            errorMessage = "Receivable pagination is invalid.";
            return false;
        }

        var sortBy = NormalizeSortBy(query.SortBy);
        if (!ReceivableSortFields.Contains(sortBy))
        {
            errorMessage = "Receivable sort field is invalid.";
            return false;
        }

        var sortDirection = NormalizeSortDirection(query.SortDirection);
        if (sortDirection is not SortAscending and not SortDescending)
        {
            errorMessage = "Receivable sort direction is invalid.";
            return false;
        }

        if (!TryResolveOptionalDateRange(query.From, query.To, out var fromUtc, out var toUtcExclusive))
        {
            errorMessage = "Receivable date range is invalid.";
            return false;
        }

        readQuery = new AdminFinancialReceivablesQueryReadModel
        {
            ProjectId = query.ProjectId,
            CustomerId = query.CustomerId,
            SalesId = query.SalesId,
            PaymentType = query.PaymentType,
            PaymentStatus = query.PaymentStatus,
            OrderStatus = query.OrderStatus,
            FromUtc = fromUtc,
            ToUtcExclusive = toUtcExclusive,
            Page = query.Page,
            PageSize = query.PageSize,
            SortBy = sortBy,
            SortDirection = sortDirection
        };
        errorMessage = string.Empty;
        return true;
    }

    private static bool TryCreateProjectsReadQuery(
        AdminFinancialProjectsQueryDto query,
        out AdminFinancialProjectsQueryReadModel readQuery,
        out string errorMessage)
    {
        readQuery = new AdminFinancialProjectsQueryReadModel();
        if (query.Page <= 0 || query.PageSize <= 0 || query.PageSize > MaxPageSize)
        {
            errorMessage = "Project financial pagination is invalid.";
            return false;
        }

        var sortBy = NormalizeProjectSortBy(query.SortBy);
        if (!ProjectSortFields.Contains(sortBy))
        {
            errorMessage = "Project financial sort field is invalid.";
            return false;
        }

        var sortDirection = NormalizeSortDirection(query.SortDirection);
        if (sortDirection is not SortAscending and not SortDescending)
        {
            errorMessage = "Project financial sort direction is invalid.";
            return false;
        }

        if (!TryResolveOptionalDateRange(query.From, query.To, out var fromUtc, out var toUtcExclusive))
        {
            errorMessage = "Project financial date range is invalid.";
            return false;
        }

        readQuery = new AdminFinancialProjectsQueryReadModel
        {
            Keyword = query.Keyword?.Trim(),
            ProjectStatus = query.ProjectStatus,
            CustomerId = query.CustomerId,
            SalesId = query.SalesId,
            PaymentStatus = query.PaymentStatus,
            PaymentType = query.PaymentType,
            HasOrder = query.HasOrder,
            HasOutstandingPayment = query.HasOutstandingPayment,
            HasReceivable = query.HasReceivable,
            FromUtc = fromUtc,
            ToUtcExclusive = toUtcExclusive,
            Page = query.Page,
            PageSize = query.PageSize,
            SortBy = sortBy,
            SortDirection = sortDirection
        };
        errorMessage = string.Empty;
        return true;
    }

    private static bool TryCreatePaymentsReadQuery(
        AdminFinancialPaymentsQueryDto query,
        out AdminFinancialPaymentsQueryReadModel readQuery,
        out string errorMessage)
    {
        readQuery = new AdminFinancialPaymentsQueryReadModel();
        if (!IsValidPage(query.Page, query.PageSize))
        {
            errorMessage = "Financial payment pagination is invalid.";
            return false;
        }

        if (query.MinFailedAttemptCount is < 0)
        {
            errorMessage = "Financial payment failed attempt filter is invalid.";
            return false;
        }

        var currency = FinancialReportingPeriodResolver.NormalizeCurrency(query.Currency);
        if (query.Currency is not null && currency != FinancialReportingConstants.DefaultCurrency)
        {
            errorMessage = "Financial payment currency is invalid.";
            return false;
        }

        var sortBy = NormalizePaymentSortBy(query.SortBy);
        if (!PaymentSortFields.Contains(sortBy))
        {
            errorMessage = "Financial payment sort field is invalid.";
            return false;
        }

        var sortDirection = NormalizeSortDirection(query.SortDirection);
        if (sortDirection is not SortAscending and not SortDescending)
        {
            errorMessage = "Financial payment sort direction is invalid.";
            return false;
        }

        if (!TryResolveOptionalDateRange(query.CreatedFrom, query.CreatedTo, out var createdFromUtc, out var createdToUtc) ||
            !TryResolveOptionalDateRange(query.PaidFrom, query.PaidTo, out var paidFromUtc, out var paidToUtc) ||
            !TryResolveOptionalDateRange(query.ExpiredFrom, query.ExpiredTo, out var expiredFromUtc, out var expiredToUtc))
        {
            errorMessage = "Financial payment date range is invalid.";
            return false;
        }

        readQuery = new AdminFinancialPaymentsQueryReadModel
        {
            ProjectId = query.ProjectId,
            OrderId = query.OrderId,
            CustomerId = query.CustomerId,
            PaymentType = query.PaymentType,
            PaymentStatus = query.PaymentStatus,
            Provider = query.Provider,
            Currency = string.IsNullOrWhiteSpace(query.Currency) ? null : currency,
            CreatedFromUtc = createdFromUtc,
            CreatedToUtcExclusive = createdToUtc,
            PaidFromUtc = paidFromUtc,
            PaidToUtcExclusive = paidToUtc,
            ExpiredFromUtc = expiredFromUtc,
            ExpiredToUtcExclusive = expiredToUtc,
            HasFailedAttempt = query.HasFailedAttempt,
            MinFailedAttemptCount = query.MinFailedAttemptCount,
            Page = query.Page,
            PageSize = query.PageSize,
            SortBy = sortBy,
            SortDirection = sortDirection
        };
        errorMessage = string.Empty;
        return true;
    }

    private static bool TryCreateExceptionsReadQuery(
        AdminFinancialExceptionsQueryDto query,
        out AdminFinancialExceptionsQueryReadModel readQuery,
        out string errorCode,
        out string errorMessage)
    {
        readQuery = new AdminFinancialExceptionsQueryReadModel();
        if (!IsValidPage(query.Page, query.PageSize) ||
            !TryResolveOptionalDateRange(query.From, query.To, out var fromUtc, out var toUtcExclusive))
        {
            errorCode = AdminFinancialErrorCodes.PaymentFilterInvalid;
            errorMessage = "Financial exception filter is invalid.";
            return false;
        }

        var exceptionType = NormalizeOptionalUpper(query.ExceptionType);
        if (!string.IsNullOrWhiteSpace(exceptionType) && !ExceptionTypes.Contains(exceptionType))
        {
            errorCode = AdminFinancialErrorCodes.ExceptionTypeInvalid;
            errorMessage = "Financial exception type is invalid.";
            return false;
        }

        readQuery = new AdminFinancialExceptionsQueryReadModel
        {
            ExceptionType = exceptionType,
            Severity = NormalizeOptionalUpper(query.Severity),
            ProjectId = query.ProjectId,
            PaymentType = query.PaymentType,
            FromUtc = fromUtc,
            ToUtcExclusive = toUtcExclusive,
            Page = query.Page,
            PageSize = query.PageSize
        };
        errorCode = string.Empty;
        errorMessage = string.Empty;
        return true;
    }

    private static bool TryResolveOptionalDateRange(
        DateTimeOffset? from,
        DateTimeOffset? to,
        out DateTime? fromUtc,
        out DateTime? toUtcExclusive)
    {
        fromUtc = null;
        toUtcExclusive = null;
        if (!from.HasValue && !to.HasValue)
        {
            return true;
        }

        if (!from.HasValue || !to.HasValue || from.Value > to.Value)
        {
            return false;
        }

        fromUtc = from.Value.UtcDateTime;
        toUtcExclusive = IsStartOfDay(to.Value)
            ? new DateTimeOffset(to.Value.Date.AddDays(1), to.Value.Offset).UtcDateTime
            : to.Value.AddTicks(1).UtcDateTime;
        return true;
    }

    private static bool TryResolveFinancialRange(
        DateTimeOffset? from,
        DateTimeOffset? to,
        out DateTime? fromUtc,
        out DateTime? toUtcExclusive)
    {
        if (!from.HasValue || !to.HasValue)
        {
            fromUtc = null;
            toUtcExclusive = null;
            return false;
        }

        return TryResolveOptionalDateRange(from, to, out fromUtc, out toUtcExclusive);
    }

    private static string NormalizeGranularity(string? granularity)
    {
        return string.IsNullOrWhiteSpace(granularity)
            ? GranularityMonth
            : granularity.Trim().ToUpperInvariant();
    }

    private static bool IsSupportedCurrency(string currency)
    {
        return string.Equals(currency, FinancialReportingConstants.DefaultCurrency, StringComparison.Ordinal);
    }

    private static decimal GetAmount(IReadOnlyList<AdminFinancialPaymentTypeAmountReadModel> rows, PaymentType type)
    {
        return rows.FirstOrDefault(row => row.PaymentType == type)?.Amount ?? 0m;
    }

    private static DateTime MaxUtc(DateTime first, DateTime second)
    {
        return first >= second ? first : second;
    }

    private static DateTime MinUtc(DateTime first, DateTime second)
    {
        return first <= second ? first : second;
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy)
            ? "confirmedAt"
            : sortBy.Trim();
    }

    private static string NormalizeProjectSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy)
            ? CreatedAtSortField
            : sortBy.Trim();
    }

    private static string NormalizePaymentSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy)
            ? CreatedAtSortField
            : sortBy.Trim();
    }

    private static string? NormalizeOptionalUpper(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpperInvariant();
    }

    private static string NormalizeSortDirection(string? sortDirection)
    {
        return string.IsNullOrWhiteSpace(sortDirection)
            ? SortDescending
            : sortDirection.Trim().ToLowerInvariant();
    }

    private static int CalculateTotalPages(int totalItems, int pageSize)
    {
        return totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
    }

    private static bool IsValidPage(int page, int pageSize)
    {
        return page > 0 && pageSize > 0 && pageSize <= MaxPageSize;
    }

    private static bool IsStartOfDay(DateTimeOffset value)
    {
        return value.TimeOfDay == TimeSpan.Zero;
    }
}
