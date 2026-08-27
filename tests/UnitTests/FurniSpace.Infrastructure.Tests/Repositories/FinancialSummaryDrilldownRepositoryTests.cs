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

public sealed class FinancialSummaryDrilldownRepositoryTests
{
    private static readonly PaymentType[] Canonical =
    [
        PaymentType.PROJECT_START_FEE,
        PaymentType.DEPOSIT,
        PaymentType.REMAINING_PAYMENT
    ];

    [Fact]
    public async Task CollectedDrilldown_TotalMatchesSummaryCollectedAmount()
    {
        await using var context = CreateContext();
        var from = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);
        var seed = await SeedAsync(context, from, now);
        var repository = new FinancialReadRepository(context);

        var summary = await repository.GetAdminSummaryAsync(from, to, now, "VND", Canonical);
        var drilldown = await repository.GetSummaryDrilldownAsync(
            new AdminFinancialSummaryDrilldownQueryReadModel
            {
                Metric = "COLLECTED",
                Page = 1,
                PageSize = 10,
                SortBy = "occurredAt",
                SortDirection = "desc"
            },
            from,
            to,
            now,
            "VND",
            Canonical);

        Assert.Equal(summary.CollectedAmount, drilldown.TotalAmount);
        Assert.Equal(3, drilldown.TotalCount);
        Assert.Contains(drilldown.Breakdowns, b => b.Dimension == "PAYMENT_TYPE");
        Assert.Contains(drilldown.Breakdowns, b => b.Dimension == "PROJECT");
        Assert.Contains(drilldown.Breakdowns, b => b.Dimension == "PROVIDER");
        Assert.All(drilldown.Items, i => Assert.Equal("PAYMENT", i.ResourceType));
        Assert.Contains(drilldown.Items, i => i.PaymentId == seed.PaidStartFeeId);
    }

    [Fact]
    public async Task CollectedDrilldown_GroupByProject_AggregatesPaymentTypeAmounts()
    {
        await using var context = CreateContext();
        var from = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);
        var seed = await SeedAsync(context, from, now);
        context.PaymentSet.Add(Payment(
            Guid.NewGuid(),
            seed.ProjectId,
            seed.OrderId,
            PaymentType.FULL_PAYMENT,
            PaymentStatus.PAID,
            50m,
            from.AddDays(6)));
        await context.SaveChangesAsync();
        var repository = new FinancialReadRepository(context);

        var summary = await repository.GetAdminSummaryAsync(from, to, now, "VND", Canonical);
        var drilldown = await repository.GetSummaryDrilldownAsync(
            new AdminFinancialSummaryDrilldownQueryReadModel
            {
                Metric = "COLLECTED",
                GroupBy = "PROJECT",
                Page = 1,
                PageSize = 10,
                SortBy = "totalCollectedAmount",
                SortDirection = "desc"
            },
            from,
            to,
            now,
            "VND",
            Canonical);

        Assert.Equal(summary.CollectedAmount, drilldown.TotalAmount);
        Assert.Equal(1, drilldown.TotalItems);
        var project = Assert.Single(drilldown.Items);
        Assert.Equal("PROJECT", project.ResourceType);
        Assert.Equal(seed.ProjectId, project.ProjectId);
        Assert.Equal(100m, project.ProjectStartFeeAmount);
        Assert.Equal(200m, project.DepositAmount);
        Assert.Equal(300m, project.RemainingPaymentAmount);
        Assert.Equal(50m, project.FullPaymentAmount);
        Assert.Equal(650m, project.TotalCollectedAmount);
        Assert.Equal(
            project.ProjectStartFeeAmount +
            project.DepositAmount +
            project.RemainingPaymentAmount +
            project.FullPaymentAmount,
            project.TotalCollectedAmount);
        Assert.Equal(4, project.PaymentCount);
        Assert.Equal(project.TotalCollectedAmount, project.Amount);
        Assert.NotNull(project.LastPaidAt);
        Assert.Equal(seed.ProjectId, project.ProjectId);
        Assert.NotNull(project.CustomerId);
        Assert.Equal(seed.OrderId, project.OrderId);
        Assert.Equal(1100m, project.OrderFinalTotal);
        Assert.Equal(400m, project.OrderPaidAmount);
        Assert.Equal(700m, project.OrderRemainingAmount);
    }

    [Fact]
    public async Task OutstandingAndActive_MatchSummaryKpis()
    {
        await using var context = CreateContext();
        var from = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);
        await SeedAsync(context, from, now);
        var repository = new FinancialReadRepository(context);

        var summary = await repository.GetAdminSummaryAsync(from, to, now, "VND", Canonical);
        var outstanding = await repository.GetSummaryDrilldownAsync(
            new AdminFinancialSummaryDrilldownQueryReadModel { Metric = "OUTSTANDING", Page = 1, PageSize = 20 },
            from, to, now, "VND", Canonical);
        var active = await repository.GetSummaryDrilldownAsync(
            new AdminFinancialSummaryDrilldownQueryReadModel { Metric = "ACTIVE_PAYMENTS", Page = 1, PageSize = 20 },
            from, to, now, "VND", Canonical);

        Assert.Equal(summary.OutstandingPaymentAmount, outstanding.TotalAmount);
        Assert.Equal(summary.ActivePaymentCount, active.TotalCount);
        Assert.Equal(summary.OutstandingPaymentAmount, active.TotalAmount);
        Assert.Contains(outstanding.Breakdowns, b => b.Dimension == "AGING");
    }

    [Fact]
    public async Task ContractedReceivableAndOrderValue_MatchSummary()
    {
        await using var context = CreateContext();
        var from = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);
        await SeedAsync(context, from, now);
        var repository = new FinancialReadRepository(context);

        var summary = await repository.GetAdminSummaryAsync(from, to, now, "VND", Canonical);
        var receivable = await repository.GetSummaryDrilldownAsync(
            new AdminFinancialSummaryDrilldownQueryReadModel { Metric = "CONTRACTED_RECEIVABLE", Page = 1, PageSize = 20 },
            from, to, now, "VND", Canonical);
        var orderValue = await repository.GetSummaryDrilldownAsync(
            new AdminFinancialSummaryDrilldownQueryReadModel { Metric = "ORDER_VALUE", Page = 1, PageSize = 20 },
            from, to, now, "VND", Canonical);

        Assert.Equal(summary.ContractedReceivableAmount, receivable.TotalAmount);
        Assert.Equal(summary.OrderCommercialValue, orderValue.TotalAmount);
        Assert.All(receivable.Items, i => Assert.Equal("ORDER", i.ResourceType));
        Assert.All(orderValue.Items, i => Assert.NotNull(i.OrderId));
    }

    [Fact]
    public async Task FailedTransactions_CountMatchesSummary()
    {
        await using var context = CreateContext();
        var from = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);
        await SeedAsync(context, from, now);
        var repository = new FinancialReadRepository(context);

        var summary = await repository.GetAdminSummaryAsync(from, to, now, "VND", Canonical);
        var failed = await repository.GetSummaryDrilldownAsync(
            new AdminFinancialSummaryDrilldownQueryReadModel { Metric = "FAILED_TRANSACTIONS", Page = 1, PageSize = 20 },
            from, to, now, "VND", Canonical);

        Assert.Equal(summary.FailedTransactionCount, failed.TotalCount);
        Assert.All(failed.Items, i =>
        {
            Assert.Equal("TRANSACTION", i.ResourceType);
            Assert.NotNull(i.TransactionId);
            Assert.NotNull(i.PaymentId);
        });
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<SeedIds> SeedAsync(AppDbContext context, DateTime periodStart, DateTime now)
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var quotationId = Guid.NewGuid();
        var paidStartFeeId = Guid.NewGuid();
        var paidDepositId = Guid.NewGuid();
        var paidRemainingId = Guid.NewGuid();
        var activePaymentId = Guid.NewGuid();
        var expiredActiveId = Guid.NewGuid();

        context.RoleSet.Add(new Role { RoleId = Guid.NewGuid(), RoleName = "CUSTOMER", Description = "c" });
        var roleId = context.RoleSet.Local.First().RoleId;
        context.AccountSet.Add(new Account
        {
            AccountId = customerId,
            RoleId = roleId,
            Email = "c@test.com",
            PasswordHash = "x",
            FullName = "Customer",
            Status = AccountStatus.ACTIVE,
            CreatedAt = now
        });
        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            ProjectCode = "PRJ-DD-001",
            ProjectName = "Drilldown Project",
            FurnitureRequirement = "Tables",
            Status = ProjectStatus.IN_PRODUCTION,
            SubmittedAt = periodStart,
            CreatedAt = periodStart
        });
        context.QuotationSet.Add(new Quotation
        {
            QuotationId = quotationId,
            ProjectId = projectId,
            ProposalId = Guid.NewGuid(),
            QuotationCode = "QT-DD",
            SubtotalAmount = 1000,
            TotalDiscountAmount = 0,
            PreVatAmount = 1000,
            VatRate = 0.1m,
            VatAmount = 100,
            TotalAmount = 1100,
            DepositAmount = 300,
            Status = QuotationStatus.ACCEPTED,
            CreatedAt = periodStart
        });
        context.OrderSet.Add(new Order
        {
            OrderId = orderId,
            ProjectId = projectId,
            QuotationId = quotationId,
            OrderCode = "ORD-DD-001",
            CustomerId = customerId,
            VatRate = 0.1m,
            VatAmount = 100,
            OriginalTotalAmount = 1000,
            FinalTotalAmount = 1100,
            PaidAmount = 400,
            RemainingAmount = 700,
            Status = OrderStatus.IN_PRODUCTION,
            ConfirmedAt = periodStart.AddDays(2),
            CreatedAt = periodStart.AddDays(2)
        });

        context.PaymentSet.AddRange(
            Payment(paidStartFeeId, projectId, null, PaymentType.PROJECT_START_FEE, PaymentStatus.PAID, 100m, periodStart.AddDays(1)),
            Payment(paidDepositId, projectId, orderId, PaymentType.DEPOSIT, PaymentStatus.PAID, 200m, periodStart.AddDays(3)),
            Payment(paidRemainingId, projectId, orderId, PaymentType.REMAINING_PAYMENT, PaymentStatus.PAID, 300m, periodStart.AddDays(5)),
            Payment(activePaymentId, projectId, orderId, PaymentType.REMAINING_PAYMENT, PaymentStatus.PENDING, 80m, null, createdAt: now.AddDays(-5)),
            Payment(expiredActiveId, projectId, orderId, PaymentType.DEPOSIT, PaymentStatus.PENDING, 50m, null, createdAt: now.AddDays(-10), expiredAt: now.AddDays(-1)));

        context.PaymentTransactionSet.AddRange(
            new PaymentTransaction
            {
                PaymentTransactionId = Guid.NewGuid(),
                PaymentId = paidDepositId,
                ProjectId = projectId,
                OrderId = orderId,
                TransactionCode = "TX-OK",
                TransactionType = PaymentTransactionType.CHARGE,
                Amount = 200m,
                Currency = "VND",
                PaymentProvider = PaymentProvider.PAYOS,
                Status = PaymentTransactionStatus.SUCCESS,
                ConfirmedAt = periodStart.AddDays(3),
                CreatedAt = periodStart.AddDays(3)
            },
            new PaymentTransaction
            {
                PaymentTransactionId = Guid.NewGuid(),
                PaymentId = activePaymentId,
                ProjectId = projectId,
                OrderId = orderId,
                TransactionCode = "TX-FAIL",
                TransactionType = PaymentTransactionType.CHARGE,
                Amount = 80m,
                Currency = "VND",
                PaymentProvider = PaymentProvider.SEPAY,
                Status = PaymentTransactionStatus.FAILED,
                FailureReason = "Insufficient funds",
                CreatedAt = periodStart.AddDays(4)
            });

        await context.SaveChangesAsync();
        return new SeedIds(projectId, orderId, paidStartFeeId);
    }

    private static Payment Payment(
        Guid id,
        Guid projectId,
        Guid? orderId,
        PaymentType type,
        PaymentStatus status,
        decimal amount,
        DateTime? paidAt,
        DateTime? createdAt = null,
        DateTime? expiredAt = null) => new()
    {
        PaymentId = id,
        ProjectId = projectId,
        OrderId = orderId,
        PaymentCode = $"PAY-{id:N}"[..12],
        PaymentType = type,
        Amount = amount,
        Currency = "VND",
        Status = status,
        PaidAt = paidAt,
        CreatedAt = createdAt ?? paidAt ?? DateTime.UtcNow,
        ExpiredAt = expiredAt
    };

    private sealed record SeedIds(Guid ProjectId, Guid OrderId, Guid PaidStartFeeId);
}
