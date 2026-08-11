#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.Financial;
using FurniSpace.Application.DTOs.Financial;
using FurniSpace.Application.Services.Financial;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Financial;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Financial;

public sealed class AdminFinancialServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_WithCustomRange_ReturnsMappedSummary()
    {
        var repository = new FakeFinancialReadRepository
        {
            Summary = new AdminFinancialSummaryReadModel
            {
                CollectedAmount = 100m,
                OutstandingPaymentAmount = 20m,
                ContractedReceivableAmount = 300m,
                OrderCommercialValue = 500m,
                FailedTransactionCount = 2,
                ActivePaymentCount = 1
            }
        };
        var service = new AdminFinancialService(repository);

        var result = await service.GetSummaryAsync(new AdminFinancialSummaryQueryDto
        {
            Period = "CUSTOM",
            From = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.FromHours(7)),
            To = new DateTimeOffset(2026, 7, 31, 0, 0, 0, TimeSpan.FromHours(7)),
            Currency = "vnd"
        });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal("CUSTOM", result.Data!.Period.Type);
        Assert.Equal("Asia/Ho_Chi_Minh", result.Data.Period.Timezone);
        Assert.Equal("VND", result.Data.Currency);
        Assert.Equal(100m, result.Data.CollectedAmount);
        Assert.Equal(20m, result.Data.OutstandingPaymentAmount);
        Assert.Equal(300m, result.Data.ContractedReceivableAmount);
        Assert.Equal(500m, result.Data.OrderCommercialValue);
        Assert.Equal(2, result.Data.FailedTransactionCount);
        Assert.Equal(1, result.Data.ActivePaymentCount);
        Assert.Equal(
            FinancialReportingConstants.CanonicalCollectedPaymentTypes,
            repository.CanonicalPaymentTypes);
    }

    [Theory]
    [InlineData("CUSTOM", null, null, AdminFinancialErrorCodes.DateRangeInvalid)]
    [InlineData("INVALID", "2026-07-01", "2026-07-31", AdminFinancialErrorCodes.PeriodInvalid)]
    public async Task GetSummaryAsync_WithInvalidPeriodInput_ReturnsBadRequest(
        string period,
        string? from,
        string? to,
        string expectedErrorCode)
    {
        var service = new AdminFinancialService(new FakeFinancialReadRepository());

        var result = await service.GetSummaryAsync(new AdminFinancialSummaryQueryDto
        {
            Period = period,
            From = Parse(from),
            To = Parse(to)
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
    }

    [Fact]
    public async Task GetSummaryAsync_WithFromAfterTo_ReturnsBadRequest()
    {
        var service = new AdminFinancialService(new FakeFinancialReadRepository());

        var result = await service.GetSummaryAsync(new AdminFinancialSummaryQueryDto
        {
            Period = "CUSTOM",
            From = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(7)),
            To = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.FromHours(7))
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(AdminFinancialErrorCodes.DateRangeInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task GetSummaryAsync_WithUnsupportedCurrency_ReturnsBadRequest()
    {
        var service = new AdminFinancialService(new FakeFinancialReadRepository());

        var result = await service.GetSummaryAsync(new AdminFinancialSummaryQueryDto
        {
            Currency = "USD"
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(AdminFinancialErrorCodes.CurrencyInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task GetSummaryAsync_WithCustomDateTimeRange_UsesExactEndInstant()
    {
        var repository = new FakeFinancialReadRepository();
        var service = new AdminFinancialService(repository);
        var to = new DateTimeOffset(2026, 7, 31, 12, 30, 0, TimeSpan.FromHours(7));

        var result = await service.GetSummaryAsync(new AdminFinancialSummaryQueryDto
        {
            Period = "CUSTOM",
            From = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.FromHours(7)),
            To = to
        });

        Assert.Equal(200, result.Status);
        Assert.Equal(to, result.Data!.Period.To);
        Assert.Equal(to.AddTicks(1).UtcDateTime, repository.ToUtcExclusive);
    }

    [Theory]
    [InlineData(null, "THIS_MONTH")]
    [InlineData("THIS_YEAR", "THIS_YEAR")]
    public async Task GetSummaryAsync_WithCurrentPeriod_ReturnsResolvedPeriod(
        string? period,
        string expectedPeriod)
    {
        var service = new AdminFinancialService(new FakeFinancialReadRepository());

        var result = await service.GetSummaryAsync(new AdminFinancialSummaryQueryDto
        {
            Period = period
        });

        Assert.Equal(200, result.Status);
        Assert.Equal(expectedPeriod, result.Data!.Period.Type);
        Assert.True(result.Data.Period.From <= result.Data.Period.To);
    }

    [Fact]
    public async Task GetReceivablesAsync_WithValidQuery_ReturnsMappedReceivables()
    {
        var repository = new FakeFinancialReadRepository
        {
            ReceivablesSummary = new AdminFinancialReceivablesSummaryReadModel
            {
                OutstandingPaymentAmount = 70m,
                OutstandingPaymentCount = 1,
                ContractedReceivableAmount = 140m,
                OrdersWithReceivableCount = 2
            },
            ReceivableItems =
            [
                new AdminFinancialReceivableItemReadModel
                {
                    ProjectId = Guid.NewGuid(),
                    ProjectCode = "PRJ-001",
                    ProjectName = "Cafe",
                    OrderId = Guid.NewGuid(),
                    OrderCode = "ORD-001",
                    OrderStatus = OrderStatus.FINAL_PAYMENT_PENDING,
                    FinalTotalAmount = 200m,
                    PaidAmount = 130m,
                    RemainingAmount = 70m,
                    ActivePaymentId = Guid.NewGuid(),
                    ActivePaymentType = PaymentType.REMAINING_PAYMENT,
                    ActivePaymentAmount = 70m,
                    ActivePaymentStatus = PaymentStatus.PENDING
                }
            ],
            ReceivableTotalItems = 21
        };
        var service = new AdminFinancialService(repository);
        var from = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.FromHours(7));
        var to = new DateTimeOffset(2026, 7, 31, 0, 0, 0, TimeSpan.FromHours(7));

        var result = await service.GetReceivablesAsync(new AdminFinancialReceivablesQueryDto
        {
            From = from,
            To = to,
            Page = 2,
            PageSize = 10,
            SortBy = "remainingAmount",
            SortDirection = "ASC",
            PaymentType = PaymentType.REMAINING_PAYMENT,
            PaymentStatus = PaymentStatus.PENDING,
            OrderStatus = OrderStatus.FINAL_PAYMENT_PENDING
        });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(70m, result.Data!.OutstandingPaymentAmount);
        Assert.Equal(1, result.Data.OutstandingPaymentCount);
        Assert.Equal(140m, result.Data.ContractedReceivableAmount);
        Assert.Equal(2, result.Data.OrdersWithReceivableCount);
        Assert.Equal(2, result.Data.Page);
        Assert.Equal(10, result.Data.PageSize);
        Assert.Equal(21, result.Data.TotalItems);
        Assert.Equal(3, result.Data.TotalPages);
        Assert.True(result.Data.Items[0].IsPaymentCreated);
        Assert.Equal("remainingAmount", repository.ReceivablesQuery!.SortBy);
        Assert.Equal("asc", repository.ReceivablesQuery.SortDirection);
        Assert.Equal(from.UtcDateTime, repository.ReceivablesQuery.FromUtc);
        Assert.Equal(new DateTimeOffset(to.Date.AddDays(1), to.Offset).UtcDateTime, repository.ReceivablesQuery.ToUtcExclusive);
    }

    [Theory]
    [InlineData(0, 20, null, null)]
    [InlineData(1, 101, null, null)]
    [InlineData(1, 20, "invalid", null)]
    [InlineData(1, 20, null, "sideways")]
    public async Task GetReceivablesAsync_WithInvalidPagingOrSort_ReturnsBadRequest(
        int page,
        int pageSize,
        string? sortBy,
        string? sortDirection)
    {
        var service = new AdminFinancialService(new FakeFinancialReadRepository());

        var result = await service.GetReceivablesAsync(new AdminFinancialReceivablesQueryDto
        {
            Page = page,
            PageSize = pageSize,
            SortBy = sortBy,
            SortDirection = sortDirection
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(AdminFinancialErrorCodes.ReceivableFilterInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task GetReceivablesAsync_WithInvalidDateRange_ReturnsBadRequest()
    {
        var service = new AdminFinancialService(new FakeFinancialReadRepository());

        var result = await service.GetReceivablesAsync(new AdminFinancialReceivablesQueryDto
        {
            From = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(7)),
            To = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.FromHours(7))
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(AdminFinancialErrorCodes.ReceivableFilterInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task GetPaymentBreakdownAsync_WithValidRange_ReturnsMappedBreakdown()
    {
        var repository = new FakeFinancialReadRepository
        {
            PaymentBreakdown =
            [
                new AdminFinancialPaymentTypeBreakdownReadModel
                {
                    PaymentType = PaymentType.PROJECT_START_FEE,
                    CollectedAmount = 100m,
                    PaidCount = 1,
                    OutstandingAmount = 20m,
                    OutstandingCount = 2,
                    ExpiredCount = 3
                }
            ]
        };
        var service = new AdminFinancialService(repository);

        var result = await service.GetPaymentBreakdownAsync(new AdminFinancialPaymentBreakdownQueryDto
        {
            From = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.FromHours(7)),
            To = new DateTimeOffset(2026, 7, 31, 0, 0, 0, TimeSpan.FromHours(7)),
            Currency = "vnd"
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("VND", result.Data!.Currency);
        Assert.Equal(PaymentType.PROJECT_START_FEE, result.Data.Items[0].PaymentType);
        Assert.Equal(100m, result.Data.Items[0].CollectedAmount);
        Assert.Equal(2, result.Data.Items[0].OutstandingCount);
        Assert.Equal(
            FinancialReportingConstants.CanonicalCollectedPaymentTypes,
            repository.PaymentBreakdownCanonicalTypes);
    }

    [Fact]
    public async Task GetCollectionTrendAsync_WithMonthlyRange_ReturnsZeroAndCollectedBuckets()
    {
        var repository = new FakeFinancialReadRepository
        {
            TrendRowsByFromUtc =
            {
                [new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.FromHours(7)).UtcDateTime] =
                [
                    new AdminFinancialPaymentTypeAmountReadModel
                    {
                        PaymentType = PaymentType.DEPOSIT,
                        Amount = 200m
                    }
                ]
            }
        };
        var service = new AdminFinancialService(repository);

        var result = await service.GetCollectionTrendAsync(new AdminFinancialCollectionTrendQueryDto
        {
            From = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.FromHours(7)),
            To = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.FromHours(7)),
            Granularity = "month"
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("MONTH", result.Data!.Granularity);
        Assert.Equal("Asia/Ho_Chi_Minh", result.Data.Timezone);
        Assert.Equal("VND", result.Data.Currency);
        Assert.Equal(2, result.Data.Series.Count);
        Assert.Equal("2026-07", result.Data.Series[0].Period);
        Assert.Equal(200m, result.Data.Series[0].Deposit);
        Assert.Equal(200m, result.Data.Series[0].Total);
        Assert.Equal("2026-08", result.Data.Series[1].Period);
        Assert.Equal(0m, result.Data.Series[1].Total);
    }

    [Theory]
    [InlineData("breakdown")]
    [InlineData("trend")]
    public async Task FinancialTrendEndpoints_WithMissingRange_ReturnBadRequest(string endpoint)
    {
        var service = new AdminFinancialService(new FakeFinancialReadRepository());

        if (endpoint == "breakdown")
        {
            var result = await service.GetPaymentBreakdownAsync(new AdminFinancialPaymentBreakdownQueryDto());

            Assert.Equal(400, result.Status);
            Assert.Equal(AdminFinancialErrorCodes.DateRangeInvalid, result.ErrorCode);
            return;
        }

        var trendResult = await service.GetCollectionTrendAsync(new AdminFinancialCollectionTrendQueryDto());

        Assert.Equal(400, trendResult.Status);
        Assert.Equal(AdminFinancialErrorCodes.DateRangeInvalid, trendResult.ErrorCode);
    }

    [Fact]
    public async Task GetCollectionTrendAsync_WithInvalidGranularity_ReturnsBadRequest()
    {
        var service = new AdminFinancialService(new FakeFinancialReadRepository());

        var result = await service.GetCollectionTrendAsync(new AdminFinancialCollectionTrendQueryDto
        {
            From = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.FromHours(7)),
            To = new DateTimeOffset(2026, 7, 31, 0, 0, 0, TimeSpan.FromHours(7)),
            Granularity = "DAY"
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(AdminFinancialErrorCodes.GranularityInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task GetCollectionTrendAsync_WithUnsupportedCurrency_ReturnsBadRequest()
    {
        var service = new AdminFinancialService(new FakeFinancialReadRepository());

        var result = await service.GetCollectionTrendAsync(new AdminFinancialCollectionTrendQueryDto
        {
            From = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.FromHours(7)),
            To = new DateTimeOffset(2026, 7, 31, 0, 0, 0, TimeSpan.FromHours(7)),
            Currency = "USD"
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(AdminFinancialErrorCodes.CurrencyInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task GetProjectsAsync_WithValidQuery_ReturnsMappedRows()
    {
        var projectId = Guid.NewGuid();
        var repository = new FakeFinancialReadRepository
        {
            ProjectRows =
            [
                new AdminFinancialProjectRowReadModel
                {
                    ProjectId = projectId,
                    ProjectCode = "PRJ-001",
                    ProjectName = "Cafe Financial",
                    ProjectStatus = ProjectStatus.QUOTATION_SENT,
                    CustomerId = Guid.NewGuid(),
                    CustomerName = "Customer One",
                    AssignedSalesId = Guid.NewGuid(),
                    AssignedSalesName = "Sales One",
                    ProjectStartFeeAmount = 100m,
                    ProjectStartFeeStatus = PaymentStatus.PAID,
                    OrderFinalTotal = 900m,
                    OrderRemainingAmount = 300m,
                    ActivePaymentType = PaymentType.REMAINING_PAYMENT,
                    ActivePaymentStatus = PaymentStatus.PENDING,
                    TotalProjectCashCollected = 600m
                }
            ],
            ProjectTotalItems = 11
        };
        var service = new AdminFinancialService(repository);
        var from = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.FromHours(7));
        var to = new DateTimeOffset(2026, 7, 31, 0, 0, 0, TimeSpan.FromHours(7));

        var result = await service.GetProjectsAsync(new AdminFinancialProjectsQueryDto
        {
            Keyword = " cafe ",
            ProjectStatus = ProjectStatus.QUOTATION_SENT,
            PaymentType = PaymentType.REMAINING_PAYMENT,
            PaymentStatus = PaymentStatus.PENDING,
            HasOrder = true,
            HasOutstandingPayment = true,
            HasReceivable = true,
            From = from,
            To = to,
            Page = 2,
            PageSize = 5,
            SortBy = "totalProjectCashCollected",
            SortDirection = "ASC"
        });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data!.Page);
        Assert.Equal(5, result.Data.PageSize);
        Assert.Equal(11, result.Data.TotalItems);
        Assert.Equal(3, result.Data.TotalPages);
        Assert.Single(result.Data.Items);
        Assert.Equal(projectId, result.Data.Items[0].ProjectId);
        Assert.Equal(600m, result.Data.Items[0].TotalProjectCashCollected);
        Assert.Equal("cafe", repository.ProjectsQuery!.Keyword);
        Assert.Equal("totalProjectCashCollected", repository.ProjectsQuery.SortBy);
        Assert.Equal("asc", repository.ProjectsQuery.SortDirection);
        Assert.Equal(from.UtcDateTime, repository.ProjectsQuery.FromUtc);
    }

    [Theory]
    [InlineData(0, 20, null, null)]
    [InlineData(1, 101, null, null)]
    [InlineData(1, 20, "invalid", null)]
    [InlineData(1, 20, null, "sideways")]
    public async Task GetProjectsAsync_WithInvalidPagingOrSort_ReturnsBadRequest(
        int page,
        int pageSize,
        string? sortBy,
        string? sortDirection)
    {
        var service = new AdminFinancialService(new FakeFinancialReadRepository());

        var result = await service.GetProjectsAsync(new AdminFinancialProjectsQueryDto
        {
            Page = page,
            PageSize = pageSize,
            SortBy = sortBy,
            SortDirection = sortDirection
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(AdminFinancialErrorCodes.ProjectFilterInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task GetProjectsAsync_WithInvalidDateRange_ReturnsBadRequest()
    {
        var service = new AdminFinancialService(new FakeFinancialReadRepository());

        var result = await service.GetProjectsAsync(new AdminFinancialProjectsQueryDto
        {
            From = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(7)),
            To = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.FromHours(7))
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(AdminFinancialErrorCodes.ProjectFilterInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task GetProjectAsync_WhenProjectExists_ReturnsMappedDetail()
    {
        var projectId = Guid.NewGuid();
        var repository = new FakeFinancialReadRepository
        {
            ProjectDetail = new AdminFinancialProjectRowReadModel
            {
                ProjectId = projectId,
                ProjectName = "Detail Project",
                CustomerId = Guid.NewGuid(),
                TotalProjectCashCollected = 120m
            }
        };
        var service = new AdminFinancialService(repository);

        var result = await service.GetProjectAsync(projectId);

        Assert.Equal(200, result.Status);
        Assert.Equal(projectId, result.Data!.ProjectId);
        Assert.Equal(120m, result.Data.TotalProjectCashCollected);
        Assert.Equal(projectId, repository.ProjectDetailId);
    }

    [Fact]
    public async Task GetProjectAsync_WhenProjectMissing_ReturnsNotFound()
    {
        var service = new AdminFinancialService(new FakeFinancialReadRepository());

        var result = await service.GetProjectAsync(Guid.NewGuid());

        Assert.Equal(404, result.Status);
        Assert.Equal(AdminFinancialErrorCodes.ProjectNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetPaymentsAsync_WithValidQuery_ReturnsMappedDiagnostics()
    {
        var paymentId = Guid.NewGuid();
        var repository = new FakeFinancialReadRepository
        {
            PaymentRows =
            [
                new AdminFinancialPaymentRowReadModel
                {
                    PaymentId = paymentId,
                    PaymentCode = "PAY-001",
                    ProjectId = Guid.NewGuid(),
                    CustomerId = Guid.NewGuid(),
                    PaymentType = PaymentType.DEPOSIT,
                    Amount = 100m,
                    Currency = "VND",
                    Status = PaymentStatus.PENDING,
                    LastProvider = PaymentProvider.PAYOS,
                    AttemptCount = 3,
                    FailedAttemptCount = 2,
                    LastTransactionStatus = PaymentTransactionStatus.FAILED,
                    LastFailureReason = "Declined"
                }
            ],
            PaymentTotalItems = 12
        };
        var service = new AdminFinancialService(repository);
        var createdFrom = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.FromHours(7));
        var createdTo = new DateTimeOffset(2026, 7, 31, 0, 0, 0, TimeSpan.FromHours(7));

        var result = await service.GetPaymentsAsync(new AdminFinancialPaymentsQueryDto
        {
            PaymentType = PaymentType.DEPOSIT,
            PaymentStatus = PaymentStatus.PENDING,
            Provider = PaymentProvider.PAYOS,
            CreatedFrom = createdFrom,
            CreatedTo = createdTo,
            Currency = "vnd",
            HasFailedAttempt = true,
            MinFailedAttemptCount = 2,
            Page = 2,
            PageSize = 5,
            SortBy = "amount",
            SortDirection = "ASC"
        });

        Assert.Equal(200, result.Status);
        Assert.Equal(12, result.Data!.TotalItems);
        Assert.Equal(3, result.Data.TotalPages);
        Assert.Equal(paymentId, result.Data.Items[0].PaymentId);
        Assert.Equal(2, result.Data.Items[0].FailedAttemptCount);
        Assert.Equal("Declined", result.Data.Items[0].LastFailureReason);
        Assert.Equal("amount", repository.PaymentsQuery!.SortBy);
        Assert.Equal("asc", repository.PaymentsQuery.SortDirection);
        Assert.Equal("VND", repository.PaymentsQuery.Currency);
        Assert.Equal(createdFrom.UtcDateTime, repository.PaymentsQuery.CreatedFromUtc);
    }

    [Theory]
    [InlineData(0, 20, null, null, null, null)]
    [InlineData(1, 101, null, null, null, null)]
    [InlineData(1, 20, "bad", null, null, null)]
    [InlineData(1, 20, null, "sideways", null, null)]
    [InlineData(1, 20, null, null, -1, null)]
    [InlineData(1, 20, null, null, null, "USD")]
    public async Task GetPaymentsAsync_WithInvalidFilter_ReturnsBadRequest(
        int page,
        int pageSize,
        string? sortBy,
        string? sortDirection,
        int? minFailedAttemptCount,
        string? currency)
    {
        var service = new AdminFinancialService(new FakeFinancialReadRepository());

        var result = await service.GetPaymentsAsync(new AdminFinancialPaymentsQueryDto
        {
            Page = page,
            PageSize = pageSize,
            SortBy = sortBy,
            SortDirection = sortDirection,
            MinFailedAttemptCount = minFailedAttemptCount,
            Currency = currency
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(AdminFinancialErrorCodes.PaymentFilterInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task GetExceptionsAsync_WithValidQuery_ReturnsMappedRows()
    {
        var paymentId = Guid.NewGuid();
        var repository = new FakeFinancialReadRepository
        {
            ExceptionRows =
            [
                new AdminFinancialExceptionRowReadModel
                {
                    ExceptionType = "PAYMENT_REPEATED_FAILURE",
                    Severity = "HIGH",
                    ProjectId = Guid.NewGuid(),
                    PaymentId = paymentId,
                    Title = "Payment has repeated failed attempts",
                    Reason = "Two failures",
                    RecommendedAction = "Review",
                    TargetResourceType = "PAYMENT",
                    TargetResourceId = paymentId
                }
            ],
            ExceptionTotalItems = 1
        };
        var service = new AdminFinancialService(repository);

        var result = await service.GetExceptionsAsync(new AdminFinancialExceptionsQueryDto
        {
            ExceptionType = "payment_repeated_failure",
            Severity = "high",
            Page = 1,
            PageSize = 10
        });

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
        Assert.Equal("PAYMENT_REPEATED_FAILURE", repository.ExceptionsQuery!.ExceptionType);
        Assert.Equal("HIGH", repository.ExceptionsQuery.Severity);
    }

    [Theory]
    [InlineData("UNKNOWN", 1, 20, AdminFinancialErrorCodes.ExceptionTypeInvalid)]
    [InlineData(null, 0, 20, AdminFinancialErrorCodes.PaymentFilterInvalid)]
    [InlineData(null, 1, 101, AdminFinancialErrorCodes.PaymentFilterInvalid)]
    public async Task GetExceptionsAsync_WithInvalidFilter_ReturnsBadRequest(
        string? exceptionType,
        int page,
        int pageSize,
        string expectedErrorCode)
    {
        var service = new AdminFinancialService(new FakeFinancialReadRepository());

        var result = await service.GetExceptionsAsync(new AdminFinancialExceptionsQueryDto
        {
            ExceptionType = exceptionType,
            Page = page,
            PageSize = pageSize
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
    }

    private static DateTimeOffset? Parse(string? value)
    {
        return value is null
            ? null
            : DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class FakeFinancialReadRepository : IFinancialReadRepository
    {
        public AdminFinancialSummaryReadModel Summary { get; init; } = new();
        public AdminFinancialReceivablesSummaryReadModel ReceivablesSummary { get; init; } = new();
        public IReadOnlyList<AdminFinancialReceivableItemReadModel> ReceivableItems { get; init; } = [];
        public int ReceivableTotalItems { get; init; }
        public IReadOnlyList<AdminFinancialPaymentTypeBreakdownReadModel> PaymentBreakdown { get; init; } = [];
        public IReadOnlyList<AdminFinancialProjectRowReadModel> ProjectRows { get; init; } = [];
        public AdminFinancialProjectRowReadModel? ProjectDetail { get; init; }
        public int ProjectTotalItems { get; init; }
        public IReadOnlyList<AdminFinancialPaymentRowReadModel> PaymentRows { get; init; } = [];
        public int PaymentTotalItems { get; init; }
        public IReadOnlyList<AdminFinancialExceptionRowReadModel> ExceptionRows { get; init; } = [];
        public int ExceptionTotalItems { get; init; }
        public Dictionary<DateTime, IReadOnlyList<AdminFinancialPaymentTypeAmountReadModel>> TrendRowsByFromUtc { get; } = [];
        public IReadOnlyCollection<PaymentType>? CanonicalPaymentTypes { get; private set; }
        public IReadOnlyCollection<PaymentType>? PaymentBreakdownCanonicalTypes { get; private set; }
        public DateTime ToUtcExclusive { get; private set; }
        public AdminFinancialReceivablesQueryReadModel? ReceivablesQuery { get; private set; }
        public AdminFinancialProjectsQueryReadModel? ProjectsQuery { get; private set; }
        public AdminFinancialPaymentsQueryReadModel? PaymentsQuery { get; private set; }
        public AdminFinancialExceptionsQueryReadModel? ExceptionsQuery { get; private set; }
        public Guid? ProjectDetailId { get; private set; }

        public Task<AdminFinancialSummaryReadModel> GetAdminSummaryAsync(
            DateTime fromUtc,
            DateTime toUtcExclusive,
            DateTime utcNow,
            string currency,
            IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default)
        {
            ToUtcExclusive = toUtcExclusive;
            CanonicalPaymentTypes = canonicalPaymentTypes;
            return Task.FromResult(Summary);
        }

        public Task<AdminFinancialReceivablesSummaryReadModel> GetReceivablesSummaryAsync(
            AdminFinancialReceivablesQueryReadModel query,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            ReceivablesQuery = query;
            return Task.FromResult(ReceivablesSummary);
        }

        public Task<IReadOnlyList<AdminFinancialReceivableItemReadModel>> GetReceivableItemsAsync(
            AdminFinancialReceivablesQueryReadModel query,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            ReceivablesQuery = query;
            return Task.FromResult(ReceivableItems);
        }

        public Task<int> CountReceivableItemsAsync(
            AdminFinancialReceivablesQueryReadModel query,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            ReceivablesQuery = query;
            return Task.FromResult(ReceivableTotalItems);
        }

        public Task<IReadOnlyList<AdminFinancialPaymentTypeBreakdownReadModel>> GetPaymentBreakdownAsync(
            DateTime fromUtc,
            DateTime toUtcExclusive,
            DateTime utcNow,
            string currency,
            IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default)
        {
            PaymentBreakdownCanonicalTypes = canonicalPaymentTypes;
            return Task.FromResult(PaymentBreakdown);
        }

        public Task<IReadOnlyList<AdminFinancialPaymentTypeAmountReadModel>> GetCollectedAmountsByPaymentTypeAsync(
            DateTime fromUtc,
            DateTime toUtcExclusive,
            string currency,
            IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                TrendRowsByFromUtc.TryGetValue(fromUtc, out var rows)
                    ? rows
                    : []);
        }

        public Task<IReadOnlyList<AdminFinancialProjectRowReadModel>> GetProjectFinancialRowsAsync(
            AdminFinancialProjectsQueryReadModel query,
            DateTime utcNow,
            IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default)
        {
            ProjectsQuery = query;
            CanonicalPaymentTypes = canonicalPaymentTypes;
            return Task.FromResult(ProjectRows);
        }

        public Task<int> CountProjectFinancialRowsAsync(
            AdminFinancialProjectsQueryReadModel query,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            ProjectsQuery = query;
            return Task.FromResult(ProjectTotalItems);
        }

        public Task<AdminFinancialProjectRowReadModel?> GetProjectFinancialRowAsync(
            Guid projectId,
            DateTime utcNow,
            IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default)
        {
            ProjectDetailId = projectId;
            CanonicalPaymentTypes = canonicalPaymentTypes;
            return Task.FromResult(ProjectDetail);
        }

        public Task<IReadOnlyList<AdminFinancialPaymentRowReadModel>> GetOperationalPaymentsAsync(
            AdminFinancialPaymentsQueryReadModel query,
            CancellationToken cancellationToken = default)
        {
            PaymentsQuery = query;
            return Task.FromResult(PaymentRows);
        }

        public Task<int> CountOperationalPaymentsAsync(
            AdminFinancialPaymentsQueryReadModel query,
            CancellationToken cancellationToken = default)
        {
            PaymentsQuery = query;
            return Task.FromResult(PaymentTotalItems);
        }

        public Task<IReadOnlyList<AdminFinancialExceptionRowReadModel>> GetFinancialExceptionsAsync(
            AdminFinancialExceptionsQueryReadModel query,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            ExceptionsQuery = query;
            return Task.FromResult(ExceptionRows);
        }

        public Task<int> CountFinancialExceptionsAsync(
            AdminFinancialExceptionsQueryReadModel query,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            ExceptionsQuery = query;
            return Task.FromResult(ExceptionTotalItems);
        }
    }
}
