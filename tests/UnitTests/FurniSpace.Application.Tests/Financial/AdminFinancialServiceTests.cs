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

    private static DateTimeOffset? Parse(string? value)
    {
        return value is null
            ? null
            : DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class FakeFinancialReadRepository : IFinancialReadRepository
    {
        public AdminFinancialSummaryReadModel Summary { get; init; } = new();
        public IReadOnlyCollection<PaymentType>? CanonicalPaymentTypes { get; private set; }
        public DateTime ToUtcExclusive { get; private set; }

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
    }
}
