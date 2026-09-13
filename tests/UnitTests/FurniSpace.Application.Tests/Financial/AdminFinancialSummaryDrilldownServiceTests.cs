#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.Financial;
using FurniSpace.Application.Services.Financial;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Financial;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Financial;

public sealed class AdminFinancialSummaryDrilldownServiceTests
{
    [Fact]
    public async Task GetSummaryDrilldownAsync_InvalidMetric_ReturnsBadRequest()
    {
        var service = new AdminFinancialService(new FakeRepo());
        var result = await service.GetSummaryDrilldownAsync(
            "UNKNOWN",
            new AdminFinancialSummaryDrilldownQueryDto
            {
                From = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(7)),
                To = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.FromHours(7))
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(AdminFinancialErrorCodes.MetricInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task GetSummaryDrilldownAsync_MapsPeriodAndPercentages()
    {
        var repo = new FakeRepo
        {
            Drilldown = new AdminFinancialSummaryDrilldownReadModel
            {
                Metric = "COLLECTED",
                TotalAmount = 1000m,
                TotalCount = 2,
                TotalItems = 2,
                Page = 1,
                PageSize = 10,
                Breakdowns =
                [
                    new AdminFinancialDrilldownBreakdownReadModel
                    {
                        Dimension = "PAYMENT_TYPE",
                        Items =
                        [
                            new AdminFinancialDrilldownBreakdownItemReadModel
                            {
                                Key = "DEPOSIT",
                                Label = "Deposit",
                                Amount = 250m,
                                Count = 1
                            }
                        ]
                    }
                ],
                Items =
                [
                    new AdminFinancialDrilldownItemReadModel
                    {
                        ResourceType = "PAYMENT",
                        ProjectId = Guid.NewGuid(),
                        ProjectCode = "PRJ-1",
                        PaymentId = Guid.NewGuid(),
                        Amount = 250m,
                        OccurredAt = new DateTime(2026, 8, 10, 3, 0, 0, DateTimeKind.Utc)
                    }
                ]
            }
        };
        var service = new AdminFinancialService(repo);

        var result = await service.GetSummaryDrilldownAsync(
            "collected",
            new AdminFinancialSummaryDrilldownQueryDto
            {
                From = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(7)),
                To = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.FromHours(7)),
                Currency = "vnd",
                Page = 1,
                PageSize = 10
            });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal("COLLECTED", result.Data!.Metric);
        Assert.Equal("Asia/Ho_Chi_Minh", result.Data.Period.Timezone);
        Assert.Equal("VND", result.Data.Currency);
        Assert.Equal(25m, result.Data.Breakdowns[0].Items[0].Percentage);
        Assert.Single(result.Data.Items);
        Assert.Equal(TimeSpan.FromHours(7), result.Data.Items[0].OccurredAt!.Value.Offset);
    }

    [Fact]
    public async Task GetSummaryDrilldownAsync_InvalidGroupBy_ReturnsBadRequest()
    {
        var service = new AdminFinancialService(new FakeRepo());
        var result = await service.GetSummaryDrilldownAsync(
            "COLLECTED",
            new AdminFinancialSummaryDrilldownQueryDto
            {
                From = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(7)),
                To = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.FromHours(7)),
                GroupBy = "CUSTOMER"
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(AdminFinancialErrorCodes.GroupByInvalid, result.ErrorCode);
    }

    private sealed class FakeRepo : IFinancialReadRepository
    {
        public AdminFinancialSummaryDrilldownReadModel Drilldown { get; init; } = new();

        public Task<AdminFinancialSummaryReadModel> GetAdminSummaryAsync(
            DateTime fromUtc, DateTime toUtcExclusive, DateTime utcNow, string currency,
            System.Collections.Generic.IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AdminFinancialSummaryReadModel());

        public Task<AdminFinancialReceivablesSummaryReadModel> GetReceivablesSummaryAsync(
            AdminFinancialReceivablesQueryReadModel query, DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AdminFinancialReceivablesSummaryReadModel());

        public Task<System.Collections.Generic.IReadOnlyList<AdminFinancialReceivableItemReadModel>> GetReceivableItemsAsync(
            AdminFinancialReceivablesQueryReadModel query, DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<System.Collections.Generic.IReadOnlyList<AdminFinancialReceivableItemReadModel>>([]);

        public Task<int> CountReceivableItemsAsync(
            AdminFinancialReceivablesQueryReadModel query, DateTime utcNow,
            CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<AdminFinancialReceivableDetailReadModel?> GetReceivableOrderDetailAsync(
            Guid orderId, DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AdminFinancialReceivableDetailReadModel?>(null);

        public Task<System.Collections.Generic.IReadOnlyList<AdminFinancialPaymentTypeBreakdownReadModel>> GetPaymentBreakdownAsync(
            DateTime fromUtc, DateTime toUtcExclusive, DateTime utcNow, string currency,
            System.Collections.Generic.IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<System.Collections.Generic.IReadOnlyList<AdminFinancialPaymentTypeBreakdownReadModel>>([]);

        public Task<System.Collections.Generic.IReadOnlyList<AdminFinancialPaymentTypeAmountReadModel>> GetCollectedAmountsByPaymentTypeAsync(
            DateTime fromUtc, DateTime toUtcExclusive, string currency,
            System.Collections.Generic.IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<System.Collections.Generic.IReadOnlyList<AdminFinancialPaymentTypeAmountReadModel>>([]);

        public Task<System.Collections.Generic.IReadOnlyList<AdminFinancialProjectRowReadModel>> GetProjectFinancialRowsAsync(
            AdminFinancialProjectsQueryReadModel query, DateTime utcNow,
            System.Collections.Generic.IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<System.Collections.Generic.IReadOnlyList<AdminFinancialProjectRowReadModel>>([]);

        public Task<int> CountProjectFinancialRowsAsync(
            AdminFinancialProjectsQueryReadModel query, DateTime utcNow,
            CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<AdminFinancialProjectRowReadModel?> GetProjectFinancialRowAsync(
            Guid projectId, DateTime utcNow,
            System.Collections.Generic.IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AdminFinancialProjectRowReadModel?>(null);

        public Task<System.Collections.Generic.IReadOnlyList<AdminFinancialPaymentRowReadModel>> GetOperationalPaymentsAsync(
            AdminFinancialPaymentsQueryReadModel query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<System.Collections.Generic.IReadOnlyList<AdminFinancialPaymentRowReadModel>>([]);

        public Task<int> CountOperationalPaymentsAsync(
            AdminFinancialPaymentsQueryReadModel query,
            CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<System.Collections.Generic.IReadOnlyList<AdminFinancialExceptionRowReadModel>> GetFinancialExceptionsAsync(
            AdminFinancialExceptionsQueryReadModel query, DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<System.Collections.Generic.IReadOnlyList<AdminFinancialExceptionRowReadModel>>([]);

        public Task<int> CountFinancialExceptionsAsync(
            AdminFinancialExceptionsQueryReadModel query, DateTime utcNow,
            CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<AdminFinancialSummaryDrilldownReadModel> GetSummaryDrilldownAsync(
            AdminFinancialSummaryDrilldownQueryReadModel query,
            DateTime fromUtc,
            DateTime toUtcExclusive,
            DateTime utcNow,
            string currency,
            System.Collections.Generic.IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Drilldown);

        public Task<AdminFinancialProjectStatementReadModel?> GetProjectStatementAsync(
            AdminFinancialProjectStatementQueryReadModel query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AdminFinancialProjectStatementReadModel?>(null);
    }
}
