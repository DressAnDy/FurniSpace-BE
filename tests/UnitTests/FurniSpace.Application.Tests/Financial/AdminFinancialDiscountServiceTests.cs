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
                OrderAdditionalDiscountAmount = 20m,
                TotalDiscountAmount = 70m,
                NetOrderValueBeforeVat = 930m,
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
        Assert.Equal(70m, result.Data!.TotalDiscountAmount);
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

    private sealed class FakeRepo : IFinancialDiscountReadRepository
    {
        public AdminFinancialDiscountSummaryReadModel Summary { get; init; } = new();

        public Task<AdminFinancialDiscountSummaryReadModel> GetSummaryAsync(
            AdminFinancialDiscountQueryReadModel query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Summary);

        public Task<IReadOnlyList<AdminFinancialDiscountOrderMetricsReadModel>> GetOrderMetricsAsync(
            AdminFinancialDiscountQueryReadModel query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdminFinancialDiscountOrderMetricsReadModel>>([]);

        public Task<int> CountOrderMetricsAsync(
            AdminFinancialDiscountQueryReadModel query,
            CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<AdminFinancialDiscountOrderMetricsReadModel?> GetOrderMetricsByIdAsync(
            Guid orderId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AdminFinancialDiscountOrderMetricsReadModel?>(null);

        public Task<IReadOnlyList<AdminFinancialDiscountOrderItemReadModel>> GetOrderItemsAsync(
            Guid orderId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdminFinancialDiscountOrderItemReadModel>>([]);

        public Task<IReadOnlyList<AdminFinancialDiscountTrendBucketReadModel>> GetTrendAsync(
            AdminFinancialDiscountQueryReadModel query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdminFinancialDiscountTrendBucketReadModel>>([]);

        public Task<IReadOnlyList<AdminFinancialDiscountExceptionReadModel>> GetExceptionsAsync(
            AdminFinancialDiscountQueryReadModel query,
            decimal thresholdRate,
            decimal thresholdAmount,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdminFinancialDiscountExceptionReadModel>>([]);

        public Task<int> CountExceptionsAsync(
            AdminFinancialDiscountQueryReadModel query,
            decimal thresholdRate,
            decimal thresholdAmount,
            CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}
