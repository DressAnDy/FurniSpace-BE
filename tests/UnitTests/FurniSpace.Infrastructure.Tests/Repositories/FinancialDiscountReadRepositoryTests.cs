#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Financial;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class FinancialDiscountReadRepositoryTests
{
    [Fact]
    public async Task GetSummaryAsync_AggregatesDiscountMetrics()
    {
        await using var context = CreateContext();
        var data = await SeedDiscountScenarioAsync(context);
        var repository = new FinancialDiscountReadRepository(context);
        var query = CreateQuery(data.PeriodStart, data.PeriodEnd);

        var summary = await repository.GetSummaryAsync(query);

        Assert.Equal(2000m, summary.GrossOrderValue);
        Assert.Equal(150m, summary.ItemDiscountAmount);
        Assert.Equal(150m, summary.TotalDiscountAmount);
        Assert.Equal(1, summary.DiscountedOrderCount);
        Assert.Equal(1, summary.TotalOrderCount);
    }

    [Fact]
    public async Task GetOrderMetricsAsync_FiltersByHasDiscountAndSorts()
    {
        await using var context = CreateContext();
        var data = await SeedDiscountScenarioAsync(context);
        var repository = new FinancialDiscountReadRepository(context);
        var query = CreateQuery(data.PeriodStart, data.PeriodEnd);
        query.HasDiscount = true;
        query.SortBy = "totalDiscountAmount";
        query.SortDirection = "desc";

        var rows = await repository.GetOrderMetricsAsync(query);
        var total = await repository.CountOrderMetricsAsync(query);

        Assert.Equal(1, total);
        var row = Assert.Single(rows);
        Assert.Equal(data.OrderId, row.OrderId);
        Assert.Equal(150m, row.TotalDiscountAmount);
    }

    [Fact]
    public async Task GetOrderMetricsByIdAsync_ReturnsOrderMetrics()
    {
        await using var context = CreateContext();
        var data = await SeedDiscountScenarioAsync(context);
        var repository = new FinancialDiscountReadRepository(context);

        var row = await repository.GetOrderMetricsByIdAsync(data.OrderId);

        Assert.NotNull(row);
        Assert.Equal("ORD-DISC", row!.OrderCode);
        Assert.Equal(150m, row.TotalDiscountAmount);
    }

    [Fact]
    public async Task GetOrderItemsAsync_ReturnsLineItems()
    {
        await using var context = CreateContext();
        var data = await SeedDiscountScenarioAsync(context);
        var repository = new FinancialDiscountReadRepository(context);

        var items = await repository.GetOrderItemsAsync(data.OrderId);

        Assert.Equal(2, items.Count);
        var counter = Assert.Single(items, item => item.ProductName == "Counter");
        Assert.Equal(1000m, counter.LineGrossAmount);
        Assert.Equal(100m, counter.DiscountAmount);
    }

    [Fact]
    public async Task GetTrendAsync_GroupsByConfirmedMonth()
    {
        await using var context = CreateContext();
        var data = await SeedDiscountScenarioAsync(context);
        var repository = new FinancialDiscountReadRepository(context);
        var query = CreateQuery(data.PeriodStart, data.PeriodEnd);

        var buckets = await repository.GetTrendAsync(query);

        var bucket = Assert.Single(buckets);
        Assert.Equal("2026-08", bucket.Period);
        Assert.Equal(2000m, bucket.GrossOrderValue);
        Assert.Equal(150m, bucket.TotalDiscountAmount);
    }

    [Fact]
    public async Task GetExceptionsAsync_ReturnsHighDiscountRateAndAmount()
    {
        await using var context = CreateContext();
        var data = await SeedDiscountScenarioAsync(context);
        var repository = new FinancialDiscountReadRepository(context);
        var query = CreateQuery(data.PeriodStart, data.PeriodEnd);

        var exceptions = await repository.GetExceptionsAsync(query, thresholdRate: 5m, thresholdAmount: 100m);
        var total = await repository.CountExceptionsAsync(query, thresholdRate: 5m, thresholdAmount: 100m);

        Assert.Equal(2, exceptions.Count);
        Assert.Equal(2, total);
        Assert.Contains(exceptions, row => row.ExceptionType == "HIGH_DISCOUNT_RATE");
        Assert.Contains(exceptions, row => row.ExceptionType == "HIGH_DISCOUNT_AMOUNT");
    }

    private static AdminFinancialDiscountQueryReadModel CreateQuery(DateTime fromUtc, DateTime toUtcExclusive) =>
        new()
        {
            FromUtc = fromUtc,
            ToUtcExclusive = toUtcExclusive,
            Page = 1,
            PageSize = 20,
            SortBy = "confirmedAt",
            SortDirection = "desc"
        };

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<DiscountSeedData> SeedDiscountScenarioAsync(AppDbContext context)
    {
        var periodStart = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var customerId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var confirmedAt = new DateTime(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc);

        context.AccountSet.AddRange(
            CreateAccount(customerId, "Customer"),
            CreateAccount(salesId, "Sales"));
        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            AssignedSalesId = salesId,
            ProjectCode = "PRJ-DISC",
            ProjectName = "Discount Project",
            Status = ProjectStatus.IN_PRODUCTION,
            CreatedAt = periodStart
        });
        context.OrderSet.Add(new Order
        {
            OrderId = orderId,
            ProjectId = projectId,
            QuotationId = Guid.NewGuid(),
            OrderCode = "ORD-DISC",
            CustomerId = customerId,
            SalesId = salesId,
            Status = OrderStatus.IN_PRODUCTION,
            ConfirmedAt = confirmedAt,
            VatRate = 10m,
            VatAmount = 180m,
            FinalTotalAmount = 1980m,
            CreatedAt = confirmedAt
        });
        context.OrderItemSet.AddRange(
            new OrderItem
            {
                OrderItemId = Guid.NewGuid(),
                OrderId = orderId,
                ProductNameSnapshot = "Counter",
                Quantity = 2,
                UnitPrice = 500m,
                DiscountAmount = 100m,
                SubtotalAmount = 900m
            },
            new OrderItem
            {
                OrderItemId = Guid.NewGuid(),
                OrderId = orderId,
                ProductNameSnapshot = "Chair",
                Quantity = 1,
                UnitPrice = 1000m,
                DiscountAmount = 50m,
                SubtotalAmount = 950m
            });
        await context.SaveChangesAsync();
        return new DiscountSeedData(orderId, periodStart, periodEnd);
    }

    private static Account CreateAccount(Guid accountId, string fullName) =>
        new()
        {
            AccountId = accountId,
            RoleId = Guid.NewGuid(),
            Email = $"{fullName.ToLowerInvariant()}@example.com",
            PasswordHash = "hash",
            FullName = fullName,
            Status = AccountStatus.ACTIVE
        };

    private sealed record DiscountSeedData(Guid OrderId, DateTime PeriodStart, DateTime PeriodEnd);
}
