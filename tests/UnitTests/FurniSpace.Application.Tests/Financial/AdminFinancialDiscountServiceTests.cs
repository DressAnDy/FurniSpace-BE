#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.Financial;
using FurniSpace.Application.Services.Financial;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Financial;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Financial;

public sealed class AdminFinancialDiscountServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_ComputesAggregates()
    {
        var repo = new FakeRepo
        {
            Summary = new AdminFinancialDiscountSummaryReadModel
            {
                GrossOrderValue = 1000m,
                ItemDiscountAmount = 50m,
                TotalDiscountAmount = 50m,
                NetOrderValueBeforeVat = 950m,
                VatAmount = 93m,
                FinalOrderValue = 1023m,
                AverageDiscountRate = 7m,
                DiscountedOrderCount = 2,
                TotalOrderCount = 3
            }
        };

        var result = await new AdminFinancialDiscountService(repo).GetSummaryAsync(
            new AdminFinancialDiscountSummaryQueryDto
            {
                From = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(7)),
                To = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.FromHours(7))
            });

        Assert.Equal(200, result.Status);
        Assert.Equal(50m, result.Data!.TotalDiscountAmount);
        Assert.Equal(3, result.Data.TotalOrderCount);
    }

    [Fact]
    public async Task GetProjectsAsync_InvalidSort_ReturnsBadRequest()
    {
        var result = await new AdminFinancialDiscountService(new FakeRepo()).GetProjectsAsync(
            new AdminFinancialDiscountProjectsQueryDto
            {
                From = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(7)),
                To = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.FromHours(7)),
                SortBy = "invalid"
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(AdminFinancialDiscountErrorCodes.FilterInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task GetSummaryAsync_InvalidDateRange_ReturnsBadRequest()
    {
        var result = await new AdminFinancialDiscountService(new FakeRepo()).GetSummaryAsync(
            new AdminFinancialDiscountSummaryQueryDto());

        Assert.Equal(400, result.Status);
        Assert.Equal(AdminFinancialDiscountErrorCodes.DateRangeInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task GetSummaryAsync_InvalidCurrency_ReturnsBadRequest()
    {
        var result = await new AdminFinancialDiscountService(new FakeRepo()).GetSummaryAsync(
            new AdminFinancialDiscountSummaryQueryDto
            {
                From = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(7)),
                To = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.FromHours(7)),
                Currency = "USD"
            });

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task GetProjectsAsync_ReturnsPagedRows()
    {
        var orderId = Guid.NewGuid();
        var repo = new FakeRepo
        {
            OrderMetrics =
            [
                new AdminFinancialDiscountOrderMetricsReadModel
                {
                    OrderId = orderId,
                    ProjectName = "Cafe",
                    TotalDiscountAmount = 100m,
                    DiscountRate = 10m,
                    FinalOrderValue = 900m
                }
            ],
            OrderMetricsCount = 1
        };

        var result = await new AdminFinancialDiscountService(repo).GetProjectsAsync(
            new AdminFinancialDiscountProjectsQueryDto
            {
                From = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(7)),
                To = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.FromHours(7)),
                SortBy = "discountRate"
            });

        Assert.Equal(200, result.Status);
        Assert.Equal(orderId, result.Data!.Items[0].OrderId);
        Assert.Equal(1, result.Data.TotalItems);
    }

    [Fact]
    public async Task GetOrderDetailAsync_WhenFound_ReturnsDetailWithItems()
    {
        var orderId = Guid.NewGuid();
        var repo = new FakeRepo
        {
            OrderById = new AdminFinancialDiscountOrderMetricsReadModel
            {
                OrderId = orderId,
                OrderCode = "ORD-001",
                GrossOrderValue = 1000m,
                TotalDiscountAmount = 100m
            },
            OrderItems =
            [
                new AdminFinancialDiscountOrderItemReadModel
                {
                    ProductName = "Counter",
                    LineGrossAmount = 1000m
                }
            ]
        };

        var result = await new AdminFinancialDiscountService(repo).GetOrderDetailAsync(orderId);

        Assert.Equal(200, result.Status);
        Assert.Equal("ORD-001", result.Data!.OrderCode);
        Assert.Equal("Counter", result.Data.Items[0].ProductName);
    }

    [Fact]
    public async Task GetOrderDetailAsync_WhenMissing_ReturnsNotFound()
    {
        var result = await new AdminFinancialDiscountService(new FakeRepo()).GetOrderDetailAsync(Guid.NewGuid());

        Assert.Equal(404, result.Status);
        Assert.Equal(AdminFinancialDiscountErrorCodes.OrderNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetTrendAsync_ReturnsMonthlySeries()
    {
        var repo = new FakeRepo
        {
            Trend =
            [
                new AdminFinancialDiscountTrendBucketReadModel
                {
                    Period = "2026-08",
                    GrossOrderValue = 1000m,
                    TotalDiscountAmount = 100m
                }
            ]
        };

        var result = await new AdminFinancialDiscountService(repo).GetTrendAsync(
            new AdminFinancialDiscountTrendQueryDto
            {
                From = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(7)),
                To = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.FromHours(7)),
                Granularity = "MONTH"
            });

        Assert.Equal(200, result.Status);
        Assert.Equal("2026-08", result.Data!.Series[0].Period);
    }

    [Fact]
    public async Task GetTrendAsync_InvalidGranularity_ReturnsBadRequest()
    {
        var result = await new AdminFinancialDiscountService(new FakeRepo()).GetTrendAsync(
            new AdminFinancialDiscountTrendQueryDto
            {
                From = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(7)),
                To = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.FromHours(7)),
                Granularity = "DAY"
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(AdminFinancialDiscountErrorCodes.GranularityInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task GetExceptionsAsync_ReturnsPagedExceptions()
    {
        var repo = new FakeRepo
        {
            Exceptions =
            [
                new AdminFinancialDiscountExceptionReadModel
                {
                    ExceptionType = AdminFinancialDiscountExceptionTypes.HighDiscountRate,
                    Order = new AdminFinancialDiscountOrderMetricsReadModel
                    {
                        OrderId = Guid.NewGuid(),
                        DiscountRate = 25m
                    },
                    ThresholdRate = 20m,
                    ThresholdAmount = 1_000_000m
                }
            ],
            ExceptionsCount = 1
        };

        var result = await new AdminFinancialDiscountService(repo).GetExceptionsAsync(
            new AdminFinancialDiscountExceptionsQueryDto
            {
                From = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(7)),
                To = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.FromHours(7))
            });

        Assert.Equal(200, result.Status);
        Assert.Equal(AdminFinancialDiscountExceptionTypes.HighDiscountRate, result.Data!.Items[0].ExceptionType);
    }

    [Fact]
    public async Task GetExceptionsAsync_InvalidThresholds_ReturnsBadRequest()
    {
        var result = await new AdminFinancialDiscountService(new FakeRepo()).GetExceptionsAsync(
            new AdminFinancialDiscountExceptionsQueryDto
            {
                From = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(7)),
                To = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.FromHours(7)),
                ThresholdRate = -1m
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(AdminFinancialDiscountErrorCodes.FilterInvalid, result.ErrorCode);
    }

    private sealed class FakeRepo : IFinancialDiscountReadRepository
    {
        public AdminFinancialDiscountSummaryReadModel Summary { get; init; } = new();
        public IReadOnlyList<AdminFinancialDiscountOrderMetricsReadModel> OrderMetrics { get; init; } = [];
        public int OrderMetricsCount { get; init; }
        public AdminFinancialDiscountOrderMetricsReadModel? OrderById { get; init; }
        public IReadOnlyList<AdminFinancialDiscountOrderItemReadModel> OrderItems { get; init; } = [];
        public IReadOnlyList<AdminFinancialDiscountTrendBucketReadModel> Trend { get; init; } = [];
        public IReadOnlyList<AdminFinancialDiscountExceptionReadModel> Exceptions { get; init; } = [];
        public int ExceptionsCount { get; init; }

        public Task<AdminFinancialDiscountSummaryReadModel> GetSummaryAsync(
            AdminFinancialDiscountQueryReadModel query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Summary);

        public Task<IReadOnlyList<AdminFinancialDiscountOrderMetricsReadModel>> GetOrderMetricsAsync(
            AdminFinancialDiscountQueryReadModel query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(OrderMetrics);

        public Task<int> CountOrderMetricsAsync(
            AdminFinancialDiscountQueryReadModel query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(OrderMetricsCount == 0 ? OrderMetrics.Count : OrderMetricsCount);

        public Task<AdminFinancialDiscountOrderMetricsReadModel?> GetOrderMetricsByIdAsync(
            Guid orderId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(OrderById);

        public Task<IReadOnlyList<AdminFinancialDiscountOrderItemReadModel>> GetOrderItemsAsync(
            Guid orderId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(OrderItems);

        public Task<IReadOnlyList<AdminFinancialDiscountTrendBucketReadModel>> GetTrendAsync(
            AdminFinancialDiscountQueryReadModel query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Trend);

        public Task<IReadOnlyList<AdminFinancialDiscountExceptionReadModel>> GetExceptionsAsync(
            AdminFinancialDiscountQueryReadModel query,
            decimal thresholdRate,
            decimal thresholdAmount,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Exceptions);

        public Task<int> CountExceptionsAsync(
            AdminFinancialDiscountQueryReadModel query,
            decimal thresholdRate,
            decimal thresholdAmount,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ExceptionsCount == 0 ? Exceptions.Count : ExceptionsCount);
    }
}
