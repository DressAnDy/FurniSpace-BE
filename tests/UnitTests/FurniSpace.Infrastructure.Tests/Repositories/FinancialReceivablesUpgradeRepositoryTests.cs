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

public sealed class FinancialReceivablesUpgradeRepositoryTests
{
    [Fact]
    public async Task Receivables_EnrichesCollectionStateAndSummaryCounters()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc);
        var customerId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var project = CreateProject(customerId, salesId);
        var pendingOrder = CreateOrder(OrderStatus.FINAL_PAYMENT_PENDING, 70m, 200m, now.AddDays(-10), project.ProjectId, customerId, salesId);
        var notCreatedOrder = CreateOrder(OrderStatus.DELIVERED, 50m, 150m, now.AddDays(-5), project.ProjectId, customerId, salesId);
        var expiredOrder = CreateOrder(OrderStatus.DELIVERED, 40m, 140m, now.AddDays(-8), project.ProjectId, customerId, salesId);
        var failedOrder = CreateOrder(OrderStatus.IN_PRODUCTION, 30m, 130m, now.AddDays(-3), project.ProjectId, customerId, salesId);

        var pendingPayment = CreatePayment(PaymentType.REMAINING_PAYMENT, PaymentStatus.PENDING, 70m, pendingOrder.OrderId, project.ProjectId, expiredAt: now.AddDays(2));
        var expiredPayment = CreatePayment(PaymentType.REMAINING_PAYMENT, PaymentStatus.EXPIRED, 40m, expiredOrder.OrderId, project.ProjectId, expiredAt: now.AddDays(-1));
        var failedPayment = CreatePayment(PaymentType.REMAINING_PAYMENT, PaymentStatus.PENDING, 30m, failedOrder.OrderId, project.ProjectId, expiredAt: now.AddDays(2));

        context.RoleSet.Add(new Role { RoleId = Guid.NewGuid(), RoleName = "CUSTOMER", Description = "c" });
        var roleId = context.RoleSet.Local.First().RoleId;
        context.AccountSet.Add(CreateAccount(customerId, roleId, "Receivable Customer"));
        context.ProjectSet.Add(project);
        context.OrderSet.AddRange(pendingOrder, notCreatedOrder, expiredOrder, failedOrder);
        context.PaymentSet.AddRange(pendingPayment, expiredPayment, failedPayment);
        context.PaymentTransactionSet.Add(new PaymentTransaction
        {
            PaymentTransactionId = Guid.NewGuid(),
            PaymentId = failedPayment.PaymentId,
            TransactionCode = "TX-FAIL",
            TransactionType = PaymentTransactionType.CHARGE,
            Amount = 30m,
            Currency = "VND",
            Status = PaymentTransactionStatus.FAILED,
            FailureReason = "Declined",
            CreatedAt = now.AddHours(-1)
        });
        await context.SaveChangesAsync();

        var repository = new FinancialReadRepository(context);
        var query = new AdminFinancialReceivablesQueryReadModel
        {
            Page = 1,
            PageSize = 20,
            SortBy = "remainingAmount",
            SortDirection = "desc"
        };

        var summary = await repository.GetReceivablesSummaryAsync(query, now);
        var items = await repository.GetReceivableItemsAsync(query, now);

        Assert.Equal(190m, summary.ContractedReceivableAmount);
        Assert.Equal(4, summary.OrdersWithReceivableCount);
        Assert.Equal(1, summary.WithoutPaymentCount);
        // pending + failed(open with failed txn) both are "collection" related; activeCollection = PENDING only
        Assert.Equal(1, summary.ActiveCollectionCount);
        Assert.Equal(1, summary.ExpiredPaymentCount);
        Assert.Equal(1, summary.FailedPaymentCount);
        Assert.Equal(70m, summary.OutstandingPaymentAmount);

        Assert.Contains(items, i => i.OrderId == pendingOrder.OrderId && i.CollectionState == "PENDING");
        Assert.Contains(items, i => i.OrderId == notCreatedOrder.OrderId && i.CollectionState == "NOT_CREATED");
        Assert.Contains(items, i => i.OrderId == expiredOrder.OrderId && i.CollectionState == "EXPIRED");
        Assert.Contains(items, i => i.OrderId == failedOrder.OrderId && i.CollectionState == "FAILED");

        var detail = await repository.GetReceivableOrderDetailAsync(notCreatedOrder.OrderId, now);
        Assert.NotNull(detail);
        Assert.Equal("NOT_CREATED", detail!.CollectionState);
        Assert.Contains(detail.PaymentRounds, r => r.Status == "NOT_CREATED" && r.PaymentType == PaymentType.REMAINING_PAYMENT);
    }

    [Fact]
    public async Task Receivables_KeywordFilter_MatchesOrderOrProject()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc);
        var customerId = Guid.NewGuid();
        var project = CreateProject(customerId, Guid.NewGuid(), code: "PRJ-KW-9", name: "Keyword Cafe");
        var order = CreateOrder(OrderStatus.DELIVERED, 10m, 100m, now.AddDays(-2), project.ProjectId, customerId, project.AssignedSalesId, code: "ORD-KW-9");
        context.RoleSet.Add(new Role { RoleId = Guid.NewGuid(), RoleName = "CUSTOMER", Description = "c" });
        var roleId = context.RoleSet.Local.First().RoleId;
        context.AccountSet.Add(CreateAccount(customerId, roleId, "Alice Customer"));
        context.ProjectSet.Add(project);
        context.OrderSet.Add(order);
        await context.SaveChangesAsync();

        var repository = new FinancialReadRepository(context);
        var items = await repository.GetReceivableItemsAsync(
            new AdminFinancialReceivablesQueryReadModel { Keyword = "KW-9", Page = 1, PageSize = 10 },
            now);

        Assert.Single(items);
        Assert.Equal(order.OrderId, items[0].OrderId);
        Assert.Equal("Alice Customer", items[0].CustomerName);
    }

    [Fact]
    public async Task Receivables_FiltersByCollectionStateAgeAndProcessing()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc);
        var customerId = Guid.NewGuid();
        var project = CreateProject(customerId, Guid.NewGuid());
        var processingOrder = CreateOrder(OrderStatus.DELIVERED, 25m, 125m, now.AddDays(-12), project.ProjectId, customerId, project.AssignedSalesId);
        var pendingOrder = CreateOrder(OrderStatus.DELIVERED, 40m, 140m, now.AddDays(-2), project.ProjectId, customerId, project.AssignedSalesId);
        var processingPayment = CreatePayment(PaymentType.REMAINING_PAYMENT, PaymentStatus.PROCESSING, 25m, processingOrder.OrderId, project.ProjectId, expiredAt: now.AddDays(3));
        var pendingPayment = CreatePayment(PaymentType.DEPOSIT, PaymentStatus.PENDING, 40m, pendingOrder.OrderId, project.ProjectId, expiredAt: now.AddDays(3));

        context.RoleSet.Add(new Role { RoleId = Guid.NewGuid(), RoleName = "CUSTOMER", Description = "c" });
        var roleId = context.RoleSet.Local.First().RoleId;
        context.AccountSet.Add(CreateAccount(customerId, roleId, "Filter Customer"));
        context.ProjectSet.Add(project);
        context.OrderSet.AddRange(processingOrder, pendingOrder);
        context.PaymentSet.AddRange(processingPayment, pendingPayment);
        await context.SaveChangesAsync();

        var repository = new FinancialReadRepository(context);

        var processingItems = await repository.GetReceivableItemsAsync(
            new AdminFinancialReceivablesQueryReadModel
            {
                CollectionState = "PROCESSING",
                Page = 1,
                PageSize = 10,
                SortBy = "receivableAgeDays",
                SortDirection = "desc"
            },
            now);
        Assert.Single(processingItems);
        Assert.Equal(processingOrder.OrderId, processingItems[0].OrderId);
        Assert.Equal("PROCESSING", processingItems[0].CollectionState);
        Assert.Equal(PaymentStatus.PROCESSING, processingItems[0].ActivePaymentStatus);

        var aged = await repository.GetReceivableItemsAsync(
            new AdminFinancialReceivablesQueryReadModel
            {
                MinAgeDays = 10,
                MaxAgeDays = 20,
                Page = 1,
                PageSize = 10
            },
            now);
        Assert.Single(aged);
        Assert.Equal(processingOrder.OrderId, aged[0].OrderId);

        var byPaymentType = await repository.GetReceivableItemsAsync(
            new AdminFinancialReceivablesQueryReadModel
            {
                PaymentType = PaymentType.DEPOSIT,
                Page = 1,
                PageSize = 10
            },
            now);
        Assert.Single(byPaymentType);
        Assert.Equal(pendingOrder.OrderId, byPaymentType[0].OrderId);

        var detail = await repository.GetReceivableOrderDetailAsync(processingOrder.OrderId, now);
        Assert.NotNull(detail);
        Assert.Equal("PROCESSING", detail!.CollectionState);
        Assert.Equal(processingPayment.PaymentId, detail.ActivePaymentId);
        Assert.Contains(detail.PaymentRounds, r => r.PaymentId == processingPayment.PaymentId);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static Account CreateAccount(Guid id, Guid roleId, string name) => new()
    {
        AccountId = id,
        RoleId = roleId,
        Email = $"{id:N}@test.com",
        PasswordHash = "x",
        FullName = name,
        Status = AccountStatus.ACTIVE,
        CreatedAt = DateTime.UtcNow
    };

    private static Project CreateProject(
        Guid customerId,
        Guid? salesId,
        string code = "PRJ-RCV",
        string name = "Receivable Project") => new()
    {
        ProjectId = Guid.NewGuid(),
        CustomerId = customerId,
        AssignedSalesId = salesId,
        ProjectCode = code,
        ProjectName = name,
        FurnitureRequirement = "Tables",
        Status = ProjectStatus.IN_PRODUCTION,
        CreatedAt = DateTime.UtcNow
    };

    private static Order CreateOrder(
        OrderStatus status,
        decimal remaining,
        decimal finalTotal,
        DateTime confirmedAt,
        Guid projectId,
        Guid customerId,
        Guid? salesId,
        string? code = null) => new()
    {
        OrderId = Guid.NewGuid(),
        ProjectId = projectId,
        QuotationId = Guid.NewGuid(),
        OrderCode = code ?? $"ORD-{Guid.NewGuid():N}"[..12],
        CustomerId = customerId,
        SalesId = salesId,
        VatRate = 0.1m,
        VatAmount = 0m,
        OriginalTotalAmount = finalTotal,
        FinalTotalAmount = finalTotal,
        PaidAmount = finalTotal - remaining,
        RemainingAmount = remaining,
        Status = status,
        ConfirmedAt = confirmedAt,
        CreatedAt = confirmedAt
    };

    private static Payment CreatePayment(
        PaymentType type,
        PaymentStatus status,
        decimal amount,
        Guid orderId,
        Guid projectId,
        DateTime? expiredAt = null) => new()
    {
        PaymentId = Guid.NewGuid(),
        ProjectId = projectId,
        OrderId = orderId,
        PaymentCode = $"PAY-{Guid.NewGuid():N}"[..12],
        PaymentType = type,
        Amount = amount,
        Currency = "VND",
        Status = status,
        ExpiredAt = expiredAt,
        CreatedAt = DateTime.UtcNow.AddDays(-1)
    };
}
