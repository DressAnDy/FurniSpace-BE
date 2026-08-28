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

public sealed class FinancialReadRepositoryTests
{
    private static readonly PaymentType[] CanonicalPaymentTypes =
    [
        PaymentType.PROJECT_START_FEE,
        PaymentType.DEPOSIT,
        PaymentType.REMAINING_PAYMENT
    ];

    [Fact]
    public async Task GetAdminSummaryAsync_AggregatesFinancialMetricsWithCanonicalRules()
    {
        await using var context = CreateContext();
        var periodStart = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
        SeedPayments(context, periodStart, periodEnd, now);
        SeedOrders(context, periodStart, periodEnd);
        SeedTransactions(context, periodStart);
        await context.SaveChangesAsync();
        var repository = new FinancialReadRepository(context);

        var summary = await repository.GetAdminSummaryAsync(
            periodStart,
            periodEnd,
            now,
            "VND",
            CanonicalPaymentTypes);

        Assert.Equal(600m, summary.CollectedAmount);
        Assert.Equal(80m, summary.OutstandingPaymentAmount);
        Assert.Equal(2, summary.ActivePaymentCount);
        Assert.Equal(1269m, summary.ContractedReceivableAmount);
        Assert.Equal(1899m, summary.OrderCommercialValue);
        Assert.Equal(1, summary.FailedTransactionCount);
    }

    [Fact]
    public async Task GetAdminSummaryAsync_WithDifferentCurrency_UsesCurrencySpecificPaymentMetrics()
    {
        await using var context = CreateContext();
        var periodStart = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
        context.PaymentSet.Add(CreatePayment(
            PaymentType.DEPOSIT,
            PaymentStatus.PAID,
            40m,
            paidAt: periodStart.AddDays(1),
            currency: "USD"));
        context.PaymentTransactionSet.Add(CreateTransaction(PaymentTransactionStatus.FAILED, periodStart.AddDays(1), "USD"));
        await context.SaveChangesAsync();
        var repository = new FinancialReadRepository(context);

        var summary = await repository.GetAdminSummaryAsync(
            periodStart,
            periodEnd,
            now,
            "USD",
            CanonicalPaymentTypes);

        Assert.Equal(40m, summary.CollectedAmount);
        Assert.Equal(1, summary.FailedTransactionCount);
    }

    [Fact]
    public async Task GetReceivablesAsync_SeparatesActivePaymentsFromOrderReceivables()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);
        var customerId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var project = CreateProject(customerId, salesId);
        var orderWithPayment = CreateOrder(
            OrderStatus.FINAL_PAYMENT_PENDING,
            70m,
            100m,
            now.AddDays(-2),
            project.ProjectId,
            customerId,
            salesId);
        var orderWithoutPayment = CreateOrder(
            OrderStatus.IN_PRODUCTION,
            50m,
            80m,
            now.AddDays(-1),
            project.ProjectId,
            customerId,
            salesId);
        var activePayment = CreatePayment(
            PaymentType.REMAINING_PAYMENT,
            PaymentStatus.PENDING,
            70m,
            expiredAt: now.AddDays(1),
            orderId: orderWithPayment.OrderId,
            projectId: project.ProjectId,
            createdAt: now.AddHours(-1));
        context.ProjectSet.Add(project);
        context.OrderSet.AddRange(
            orderWithPayment,
            orderWithoutPayment,
            CreateOrder(OrderStatus.CANCELLED, 999m, 999m, now, project.ProjectId, customerId, salesId));
        context.PaymentSet.AddRange(
            activePayment,
            CreatePayment(PaymentType.REMAINING_PAYMENT, PaymentStatus.PAID, 999m, orderId: orderWithoutPayment.OrderId, projectId: project.ProjectId),
            CreatePayment(PaymentType.REMAINING_PAYMENT, PaymentStatus.PROCESSING, 999m, expiredAt: now.AddDays(-1), orderId: orderWithoutPayment.OrderId, projectId: project.ProjectId));
        context.PaymentTransactionSet.Add(CreateTransaction(PaymentTransactionStatus.FAILED, now, paymentId: activePayment.PaymentId));
        await context.SaveChangesAsync();
        var repository = new FinancialReadRepository(context);
        var query = new AdminFinancialReceivablesQueryReadModel
        {
            CustomerId = customerId,
            SalesId = salesId,
            Page = 1,
            PageSize = 10,
            SortBy = "remainingAmount",
            SortDirection = "desc"
        };

        var summary = await repository.GetReceivablesSummaryAsync(query, now);
        var total = await repository.CountReceivableItemsAsync(query, now);
        var items = await repository.GetReceivableItemsAsync(query, now);

        Assert.Equal(0m, summary.OutstandingPaymentAmount);
        Assert.Equal(0, summary.OutstandingPaymentCount);
        Assert.Equal(120m, summary.ContractedReceivableAmount);
        Assert.Equal(2, summary.OrdersWithReceivableCount);
        Assert.Equal(2, total);
        Assert.Equal(orderWithPayment.OrderId, items[0].OrderId);
        Assert.Equal(activePayment.PaymentId, items[0].ActivePaymentId);
        Assert.Equal(PaymentStatus.PENDING, items[0].ActivePaymentStatus);
        Assert.Equal("FAILED", items[0].CollectionState);
        Assert.Equal(1, summary.FailedPaymentCount);
        Assert.Null(items[1].ActivePaymentId);
        Assert.Equal("EXPIRED", items[1].CollectionState);
    }

    [Fact]
    public async Task GetReceivablesAsync_WithPaymentTypeFilter_ReturnsOnlyOrdersWithMatchingActivePayment()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);
        var project = CreateProject(Guid.NewGuid(), Guid.NewGuid());
        var depositOrder = CreateOrder(OrderStatus.DEPOSIT_PENDING, 30m, 100m, now, project.ProjectId, project.CustomerId, project.AssignedSalesId);
        var remainingOrder = CreateOrder(OrderStatus.FINAL_PAYMENT_PENDING, 70m, 100m, now, project.ProjectId, project.CustomerId, project.AssignedSalesId);
        context.ProjectSet.Add(project);
        context.OrderSet.AddRange(depositOrder, remainingOrder);
        context.PaymentSet.AddRange(
            CreatePayment(PaymentType.DEPOSIT, PaymentStatus.PENDING, 30m, orderId: depositOrder.OrderId, projectId: project.ProjectId),
            CreatePayment(PaymentType.REMAINING_PAYMENT, PaymentStatus.PENDING, 70m, orderId: remainingOrder.OrderId, projectId: project.ProjectId));
        await context.SaveChangesAsync();
        var repository = new FinancialReadRepository(context);
        var query = new AdminFinancialReceivablesQueryReadModel
        {
            ProjectId = project.ProjectId,
            PaymentType = PaymentType.DEPOSIT,
            Page = 1,
            PageSize = 10
        };

        var summary = await repository.GetReceivablesSummaryAsync(query, now);
        var items = await repository.GetReceivableItemsAsync(query, now);

        Assert.Equal(30m, summary.OutstandingPaymentAmount);
        Assert.Equal(30m, summary.ContractedReceivableAmount);
        Assert.Single(items);
        Assert.Equal(depositOrder.OrderId, items[0].OrderId);
        Assert.Equal(PaymentType.DEPOSIT, items[0].ActivePaymentType);
    }

    [Fact]
    public async Task GetPaymentBreakdownAsync_ReturnsCanonicalCollectedOutstandingAndExpiredRows()
    {
        await using var context = CreateContext();
        var fromUtc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);
        var successfulActivePaymentId = Guid.NewGuid();
        context.PaymentSet.AddRange(
            CreatePayment(PaymentType.PROJECT_START_FEE, PaymentStatus.PAID, 100m, paidAt: fromUtc.AddDays(1)),
            CreatePayment(PaymentType.DEPOSIT, PaymentStatus.PAID, 200m, paidAt: fromUtc.AddDays(2)),
            CreatePayment(PaymentType.REMAINING_PAYMENT, PaymentStatus.PENDING, 300m, expiredAt: now.AddDays(1)),
            CreatePayment(PaymentType.DEPOSIT, PaymentStatus.PROCESSING, 999m, paymentId: successfulActivePaymentId),
            CreatePayment(PaymentType.DEPOSIT, PaymentStatus.EXPIRED, 10m, expiredAt: fromUtc.AddDays(3)),
            CreatePayment(PaymentType.OTHER, PaymentStatus.PAID, 999m, paidAt: fromUtc.AddDays(4)),
            CreatePayment(PaymentType.FULL_PAYMENT, PaymentStatus.PAID, 999m, paidAt: fromUtc.AddDays(5)),
            CreatePayment(PaymentType.REFUND, PaymentStatus.PAID, 999m, paidAt: fromUtc.AddDays(6)));
        context.PaymentTransactionSet.Add(CreateTransaction(
            PaymentTransactionStatus.SUCCESS,
            fromUtc.AddDays(1),
            paymentId: successfulActivePaymentId));
        await context.SaveChangesAsync();
        var repository = new FinancialReadRepository(context);

        var rows = await repository.GetPaymentBreakdownAsync(fromUtc, toUtc, now, "VND", CanonicalPaymentTypes);

        Assert.Equal(3, rows.Count);
        var startFee = rows.Single(row => row.PaymentType == PaymentType.PROJECT_START_FEE);
        var deposit = rows.Single(row => row.PaymentType == PaymentType.DEPOSIT);
        var remaining = rows.Single(row => row.PaymentType == PaymentType.REMAINING_PAYMENT);
        Assert.Equal(100m, startFee.CollectedAmount);
        Assert.Equal(1, startFee.PaidCount);
        Assert.Equal(200m, deposit.CollectedAmount);
        Assert.Equal(1, deposit.ExpiredCount);
        Assert.Equal(0m, deposit.OutstandingAmount);
        Assert.Equal(300m, remaining.OutstandingAmount);
        Assert.Equal(1, remaining.OutstandingCount);
    }

    [Fact]
    public async Task GetCollectedAmountsByPaymentTypeAsync_UsesProvidedUtcBoundariesAndCanonicalTypes()
    {
        await using var context = CreateContext();
        var julyStartVietnamAsUtc = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.FromHours(7)).UtcDateTime;
        var augustStartVietnamAsUtc = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(7)).UtcDateTime;
        context.PaymentSet.AddRange(
            CreatePayment(PaymentType.DEPOSIT, PaymentStatus.PAID, 100m, paidAt: julyStartVietnamAsUtc.AddHours(1)),
            CreatePayment(PaymentType.REMAINING_PAYMENT, PaymentStatus.PAID, 200m, paidAt: augustStartVietnamAsUtc.AddTicks(-1)),
            CreatePayment(PaymentType.PROJECT_START_FEE, PaymentStatus.PAID, 999m, paidAt: julyStartVietnamAsUtc.AddTicks(-1)),
            CreatePayment(PaymentType.OTHER, PaymentStatus.PAID, 999m, paidAt: julyStartVietnamAsUtc.AddHours(2)));
        await context.SaveChangesAsync();
        var repository = new FinancialReadRepository(context);

        var rows = await repository.GetCollectedAmountsByPaymentTypeAsync(
            julyStartVietnamAsUtc,
            augustStartVietnamAsUtc,
            "VND",
            CanonicalPaymentTypes);

        Assert.Equal(2, rows.Count);
        Assert.Equal(100m, rows.Single(row => row.PaymentType == PaymentType.DEPOSIT).Amount);
        Assert.Equal(200m, rows.Single(row => row.PaymentType == PaymentType.REMAINING_PAYMENT).Amount);
    }

    [Fact]
    public async Task GetProjectFinancialRowsAsync_ReturnsProjectOrderPaymentOverview()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 7, 25, 0, 0, 0, DateTimeKind.Utc);
        var customerId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var project = CreateProject(customerId, salesId, ProjectStatus.QUOTATION_SENT, now.AddDays(-5), "Cafe Project");
        var latestOrder = CreateOrder(
            OrderStatus.FINAL_PAYMENT_PENDING,
            300m,
            900m,
            now.AddDays(-2),
            project.ProjectId,
            customerId,
            salesId);
        latestOrder.OriginalTotalAmount = 1000m;
        latestOrder.ItemAdjustmentAmount = 50m;
        latestOrder.AdditionalDiscountAmount = 150m;
        latestOrder.PaidAmount = 600m;
        var activePayment = CreatePayment(
            PaymentType.REMAINING_PAYMENT,
            PaymentStatus.PENDING,
            300m,
            expiredAt: now.AddDays(1),
            orderId: latestOrder.OrderId,
            projectId: project.ProjectId,
            createdAt: now.AddHours(-2));
        context.AccountSet.AddRange(
            CreateAccount(customerId, "Customer Alpha"),
            CreateAccount(salesId, "Sales Alpha"));
        context.ProjectSet.Add(project);
        context.OrderSet.AddRange(
            CreateOrder(OrderStatus.DEPOSIT_PAID, 1m, 10m, now.AddDays(-3), project.ProjectId, customerId, salesId),
            latestOrder);
        context.PaymentSet.AddRange(
            CreatePayment(PaymentType.PROJECT_START_FEE, PaymentStatus.PAID, 100m, paidAt: now.AddDays(-4), projectId: project.ProjectId),
            CreatePayment(PaymentType.DEPOSIT, PaymentStatus.PAID, 500m, paidAt: now.AddDays(-1), projectId: project.ProjectId),
            CreatePayment(PaymentType.OTHER, PaymentStatus.PAID, 999m, paidAt: now.AddDays(-1), projectId: project.ProjectId),
            activePayment);
        await context.SaveChangesAsync();
        var repository = new FinancialReadRepository(context);
        var query = new AdminFinancialProjectsQueryReadModel
        {
            Keyword = "Cafe",
            HasOrder = true,
            HasOutstandingPayment = true,
            HasReceivable = true,
            PaymentType = PaymentType.REMAINING_PAYMENT,
            PaymentStatus = PaymentStatus.PENDING,
            Page = 1,
            PageSize = 10,
            SortBy = "totalProjectCashCollected",
            SortDirection = "desc"
        };

        var total = await repository.CountProjectFinancialRowsAsync(query, now);
        var rows = await repository.GetProjectFinancialRowsAsync(query, now, CanonicalPaymentTypes);

        Assert.Equal(1, total);
        var row = Assert.Single(rows);
        Assert.Equal(project.ProjectId, row.ProjectId);
        Assert.Equal("Customer Alpha", row.CustomerName);
        Assert.Equal("Sales Alpha", row.AssignedSalesName);
        Assert.Equal(100m, row.ProjectStartFeeAmount);
        Assert.Equal(PaymentStatus.PAID, row.ProjectStartFeeStatus);
        Assert.Equal(latestOrder.OrderId, row.OrderId);
        Assert.Equal(1000m, row.OrderOriginalTotal);
        Assert.Equal(50m, row.OrderAdjustmentAmount);
        Assert.Equal(150m, row.OrderAdditionalDiscount);
        Assert.Equal(900m, row.OrderFinalTotal);
        Assert.Equal(600m, row.OrderPaidAmount);
        Assert.Equal(300m, row.OrderRemainingAmount);
        Assert.Equal(activePayment.PaymentId, row.ActivePaymentId);
        Assert.Equal(600m, row.TotalProjectCashCollected);
        Assert.Equal(now.AddDays(-1), row.LastPaidAt);
    }

    [Fact]
    public async Task GetProjectFinancialRowsAsync_WithNegativeExistenceFilters_ReturnsProjectsWithoutOrderOrReceivable()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 7, 25, 0, 0, 0, DateTimeKind.Utc);
        var project = CreateProject(Guid.NewGuid(), null, ProjectStatus.SUBMITTED, now.AddDays(-1), "No Order");
        context.AccountSet.Add(CreateAccount(project.CustomerId, "Customer Beta"));
        context.ProjectSet.Add(project);
        await context.SaveChangesAsync();
        var repository = new FinancialReadRepository(context);
        var query = new AdminFinancialProjectsQueryReadModel
        {
            HasOrder = false,
            HasReceivable = false,
            HasOutstandingPayment = false,
            FromUtc = now.AddDays(-2),
            ToUtcExclusive = now.AddDays(1),
            Page = 1,
            PageSize = 10,
            SortBy = "createdAt",
            SortDirection = "asc"
        };

        var rows = await repository.GetProjectFinancialRowsAsync(query, now, CanonicalPaymentTypes);

        var row = Assert.Single(rows);
        Assert.Equal(project.ProjectId, row.ProjectId);
        Assert.Null(row.OrderId);
        Assert.Null(row.ActivePaymentId);
        Assert.Equal(0m, row.TotalProjectCashCollected);
    }

    [Fact]
    public async Task GetProjectFinancialRowsAsync_SortsByCollectedInPeriod()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 7, 25, 0, 0, 0, DateTimeKind.Utc);
        var periodStart = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var lowCollectionProject = CreateProject(Guid.NewGuid(), null, ProjectStatus.IN_PRODUCTION, now.AddDays(-3), "Low Collection");
        var highCollectionProject = CreateProject(Guid.NewGuid(), null, ProjectStatus.IN_PRODUCTION, now.AddDays(-3), "High Collection");
        context.AccountSet.AddRange(
            CreateAccount(lowCollectionProject.CustomerId, "Customer Low"),
            CreateAccount(highCollectionProject.CustomerId, "Customer High"));
        context.ProjectSet.AddRange(lowCollectionProject, highCollectionProject);
        context.PaymentSet.AddRange(
            CreatePayment(PaymentType.DEPOSIT, PaymentStatus.PAID, 100m, paidAt: periodStart.AddDays(2), projectId: lowCollectionProject.ProjectId),
            CreatePayment(PaymentType.DEPOSIT, PaymentStatus.PAID, 500m, paidAt: periodStart.AddDays(3), projectId: highCollectionProject.ProjectId));
        await context.SaveChangesAsync();
        var repository = new FinancialReadRepository(context);
        var query = new AdminFinancialProjectsQueryReadModel
        {
            FromUtc = periodStart,
            ToUtcExclusive = periodEnd,
            Page = 1,
            PageSize = 10,
            SortBy = "collectedInPeriod",
            SortDirection = "desc"
        };

        var rows = await repository.GetProjectFinancialRowsAsync(query, now, CanonicalPaymentTypes);

        Assert.Equal(2, rows.Count);
        Assert.Equal(highCollectionProject.ProjectId, rows[0].ProjectId);
        Assert.Equal(500m, rows[0].CollectedInPeriod);
        Assert.Equal(periodStart.AddDays(3), rows[0].LastPaidInPeriod);
        Assert.Equal(100m, rows[1].CollectedInPeriod);
    }

    [Fact]
    public async Task GetProjectFinancialRowAsync_WhenProjectExists_ReturnsDetail()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 7, 25, 0, 0, 0, DateTimeKind.Utc);
        var project = CreateProject(Guid.NewGuid(), null, ProjectStatus.SUBMITTED, now, "Detail Project");
        context.AccountSet.Add(CreateAccount(project.CustomerId, "Customer Detail"));
        context.ProjectSet.Add(project);
        await context.SaveChangesAsync();
        var repository = new FinancialReadRepository(context);

        var row = await repository.GetProjectFinancialRowAsync(project.ProjectId, now, CanonicalPaymentTypes);
        var missing = await repository.GetProjectFinancialRowAsync(Guid.NewGuid(), now, CanonicalPaymentTypes);

        Assert.NotNull(row);
        Assert.Equal("Detail Project", row!.ProjectName);
        Assert.Null(missing);
    }

    [Fact]
    public async Task GetOperationalPaymentsAsync_ReturnsDiagnosticsAndAppliesFilters()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 7, 25, 0, 0, 0, DateTimeKind.Utc);
        var customerId = Guid.NewGuid();
        var project = CreateProject(customerId, null, createdAt: now.AddDays(-5), projectName: "Payment Ops");
        var order = CreateOrder(OrderStatus.DEPOSIT_PENDING, 70m, 100m, now.AddDays(-4), project.ProjectId, customerId, null);
        var payment = CreatePayment(
            PaymentType.DEPOSIT,
            PaymentStatus.PENDING,
            30m,
            orderId: order.OrderId,
            projectId: project.ProjectId,
            createdAt: now.AddDays(-3),
            expiredAt: now.AddDays(1));
        context.AccountSet.Add(CreateAccount(customerId, "Customer Ops"));
        context.ProjectSet.Add(project);
        context.OrderSet.Add(order);
        context.PaymentSet.AddRange(
            payment,
            CreatePayment(PaymentType.REMAINING_PAYMENT, PaymentStatus.PAID, 999m, paidAt: now, projectId: project.ProjectId));
        context.PaymentTransactionSet.AddRange(
            CreateTransaction(PaymentTransactionStatus.FAILED, now.AddDays(-2), paymentId: payment.PaymentId, provider: PaymentProvider.PAYOS, failureReason: "Declined"),
            CreateTransaction(PaymentTransactionStatus.FAILED, now.AddDays(-1), paymentId: payment.PaymentId, provider: PaymentProvider.PAYOS, failureReason: "Insufficient funds"));
        await context.SaveChangesAsync();
        var repository = new FinancialReadRepository(context);
        var query = new AdminFinancialPaymentsQueryReadModel
        {
            CustomerId = customerId,
            PaymentType = PaymentType.DEPOSIT,
            PaymentStatus = PaymentStatus.PENDING,
            Provider = PaymentProvider.PAYOS,
            CreatedFromUtc = now.AddDays(-4),
            CreatedToUtcExclusive = now.AddDays(-2),
            HasFailedAttempt = true,
            MinFailedAttemptCount = 2,
            Page = 1,
            PageSize = 10,
            SortBy = "amount",
            SortDirection = "desc"
        };

        var total = await repository.CountOperationalPaymentsAsync(query);
        var rows = await repository.GetOperationalPaymentsAsync(query);

        Assert.Equal(1, total);
        var row = Assert.Single(rows);
        Assert.Equal(payment.PaymentId, row.PaymentId);
        Assert.Equal("Customer Ops", row.CustomerName);
        Assert.Equal(order.OrderCode, row.OrderCode);
        Assert.Equal(2, row.AttemptCount);
        Assert.Equal(2, row.FailedAttemptCount);
        Assert.Equal(PaymentProvider.PAYOS, row.LastProvider);
        Assert.Equal(PaymentTransactionStatus.FAILED, row.LastTransactionStatus);
        Assert.Equal("Insufficient funds", row.LastFailureReason);
    }

    [Fact]
    public async Task GetFinancialExceptionsAsync_ReturnsPaymentAndOrderExceptionRows()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 7, 25, 0, 0, 0, DateTimeKind.Utc);
        var customerId = Guid.NewGuid();
        var project = CreateProject(customerId, null, createdAt: now.AddDays(-20));
        var expiredPayment = CreatePayment(
            PaymentType.DEPOSIT,
            PaymentStatus.EXPIRED,
            40m,
            expiredAt: now.AddDays(-3),
            projectId: project.ProjectId,
            createdAt: now.AddDays(-4));
        var repeatedFailurePayment = CreatePayment(
            PaymentType.DEPOSIT,
            PaymentStatus.PENDING,
            50m,
            projectId: project.ProjectId,
            createdAt: now.AddDays(-2));
        var stalePayment = CreatePayment(
            PaymentType.PROJECT_START_FEE,
            PaymentStatus.PENDING,
            10m,
            projectId: project.ProjectId,
            createdAt: now.AddDays(-10),
            expiredAt: now.AddDays(1));
        var finalPaymentOrder = CreateOrder(
            OrderStatus.FINAL_PAYMENT_PENDING,
            70m,
            100m,
            now.AddDays(-8),
            project.ProjectId,
            customerId,
            null);
        var deliveredOrder = CreateOrder(
            OrderStatus.DELIVERED,
            80m,
            100m,
            now.AddDays(-6),
            project.ProjectId,
            customerId,
            null);
        deliveredOrder.CustomerConfirmedDeliveryAt = now.AddDays(-5);
        context.ProjectSet.Add(project);
        context.PaymentSet.AddRange(expiredPayment, repeatedFailurePayment, stalePayment);
        context.OrderSet.AddRange(finalPaymentOrder, deliveredOrder);
        context.PaymentTransactionSet.AddRange(
            CreateTransaction(PaymentTransactionStatus.FAILED, now.AddDays(-2), paymentId: repeatedFailurePayment.PaymentId),
            CreateTransaction(PaymentTransactionStatus.FAILED, now.AddDays(-1), paymentId: repeatedFailurePayment.PaymentId),
            CreateTransaction(PaymentTransactionStatus.FAILED, now.AddDays(-1), paymentId: stalePayment.PaymentId));
        await context.SaveChangesAsync();
        var repository = new FinancialReadRepository(context);
        var query = new AdminFinancialExceptionsQueryReadModel
        {
            ProjectId = project.ProjectId,
            Page = 1,
            PageSize = 20
        };

        var total = await repository.CountFinancialExceptionsAsync(query, now);
        var rows = await repository.GetFinancialExceptionsAsync(query, now);

        Assert.Equal(5, total);
        Assert.Contains(rows, row => row.ExceptionType == "PAYMENT_EXPIRED" && row.PaymentId == expiredPayment.PaymentId);
        Assert.Contains(rows, row => row.ExceptionType == "PAYMENT_REPEATED_FAILURE" && row.PaymentId == repeatedFailurePayment.PaymentId);
        Assert.Contains(rows, row => row.ExceptionType == "PAYMENT_PENDING_TOO_LONG" && row.PaymentId == stalePayment.PaymentId);
        Assert.Contains(rows, row => row.ExceptionType == "FINAL_PAYMENT_NOT_CREATED" && row.OrderId == finalPaymentOrder.OrderId);
        Assert.Contains(rows, row => row.ExceptionType == "DELIVERED_WITH_RECEIVABLE" && row.OrderId == deliveredOrder.OrderId);
        Assert.DoesNotContain(rows, row => row.Reason.Contains("payload", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetFinancialExceptionsAsync_FiltersByTypeSeverityAndPaymentType()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 7, 25, 0, 0, 0, DateTimeKind.Utc);
        var project = CreateProject(Guid.NewGuid(), null);
        var payment = CreatePayment(
            PaymentType.DEPOSIT,
            PaymentStatus.PENDING,
            50m,
            projectId: project.ProjectId,
            createdAt: now.AddDays(-2));
        context.ProjectSet.Add(project);
        context.PaymentSet.Add(payment);
        context.PaymentTransactionSet.AddRange(
            CreateTransaction(PaymentTransactionStatus.FAILED, now.AddDays(-2), paymentId: payment.PaymentId),
            CreateTransaction(PaymentTransactionStatus.FAILED, now.AddDays(-1), paymentId: payment.PaymentId));
        await context.SaveChangesAsync();
        var repository = new FinancialReadRepository(context);
        var query = new AdminFinancialExceptionsQueryReadModel
        {
            ExceptionType = "PAYMENT_REPEATED_FAILURE",
            Severity = "HIGH",
            PaymentType = PaymentType.DEPOSIT,
            FromUtc = now.AddDays(-3),
            ToUtcExclusive = now.AddDays(1),
            Page = 1,
            PageSize = 10
        };

        var rows = await repository.GetFinancialExceptionsAsync(query, now);

        var row = Assert.Single(rows);
        Assert.Equal(payment.PaymentId, row.PaymentId);
        Assert.Equal(1, row.Age);
    }

    [Fact]
    public async Task GetOperationalPaymentsAsync_WithCanonicalPaidFilters_ReconcilesWithSummaryCollectedAmount()
    {
        await using var context = CreateContext();
        var fromUtc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);
        var project = CreateProject(Guid.NewGuid(), null, createdAt: fromUtc);
        context.ProjectSet.Add(project);
        context.PaymentSet.AddRange(
            CreatePayment(PaymentType.PROJECT_START_FEE, PaymentStatus.PAID, 100m, paidAt: fromUtc.AddDays(1), projectId: project.ProjectId),
            CreatePayment(PaymentType.DEPOSIT, PaymentStatus.PAID, 200m, paidAt: fromUtc.AddDays(2), projectId: project.ProjectId),
            CreatePayment(PaymentType.REMAINING_PAYMENT, PaymentStatus.PAID, 300m, paidAt: fromUtc.AddDays(3), projectId: project.ProjectId),
            CreatePayment(PaymentType.FULL_PAYMENT, PaymentStatus.PAID, 999m, paidAt: fromUtc.AddDays(4), projectId: project.ProjectId),
            CreatePayment(PaymentType.OTHER, PaymentStatus.PAID, 999m, paidAt: fromUtc.AddDays(5), projectId: project.ProjectId),
            CreatePayment(PaymentType.DEPOSIT, PaymentStatus.PAID, 999m, paidAt: fromUtc.AddDays(6), projectId: project.ProjectId, currency: "USD"));
        await context.SaveChangesAsync();
        var repository = new FinancialReadRepository(context);

        var summary = await repository.GetAdminSummaryAsync(
            fromUtc,
            toUtc,
            now,
            "VND",
            CanonicalPaymentTypes);
        var drillDownTotal = 0m;
        foreach (var paymentType in CanonicalPaymentTypes)
        {
            var rows = await repository.GetOperationalPaymentsAsync(new AdminFinancialPaymentsQueryReadModel
            {
                PaymentType = paymentType,
                PaymentStatus = PaymentStatus.PAID,
                Currency = "VND",
                PaidFromUtc = fromUtc,
                PaidToUtcExclusive = toUtc,
                Page = 1,
                PageSize = 50,
                SortBy = "paidAt",
                SortDirection = "asc"
            });
            drillDownTotal += rows.Sum(row => row.Amount);
        }

        Assert.Equal(summary.CollectedAmount, drillDownTotal);
    }

    [Fact]
    public void AppDbContext_ModelContainsFinancialDashboardIndexes()
    {
        using var context = CreateContext();

        Assert.True(HasIndex<Payment>(context, "idx_fin_payments_paid_reporting", "status = 'PAID' AND paid_at IS NOT NULL"));
        Assert.True(HasIndex<Payment>(context, "idx_fin_payments_active_obligations", "status IN ('PENDING', 'PROCESSING')"));
        Assert.True(HasIndex<PaymentTransaction>(context, "idx_fin_payment_transactions_failed_reporting", "status = 'FAILED'"));
        Assert.True(HasIndex<PaymentTransaction>(context, "idx_fin_payment_transactions_payment_failed_time", "status = 'FAILED'"));
        Assert.True(HasIndex<Order>(context, "idx_fin_orders_project_confirmed", null));
        Assert.True(HasIndex<Order>(context, "idx_fin_orders_receivable_status_confirmed", "remaining_amount > 0"));
    }

    private static void SeedPayments(
        AppDbContext context,
        DateTime periodStart,
        DateTime periodEnd,
        DateTime now)
    {
        var successfulPendingPaymentId = Guid.NewGuid();
        context.PaymentSet.AddRange(
            CreatePayment(PaymentType.PROJECT_START_FEE, PaymentStatus.PAID, 100m, paidAt: periodStart.AddDays(1)),
            CreatePayment(PaymentType.DEPOSIT, PaymentStatus.PAID, 200m, paidAt: periodStart.AddDays(2)),
            CreatePayment(PaymentType.REMAINING_PAYMENT, PaymentStatus.PAID, 300m, paidAt: periodStart.AddDays(3)),
            CreatePayment(PaymentType.OTHER, PaymentStatus.PAID, 999m, paidAt: periodStart.AddDays(4)),
            CreatePayment(PaymentType.FULL_PAYMENT, PaymentStatus.PAID, 999m, paidAt: periodStart.AddDays(5)),
            CreatePayment(PaymentType.REFUND, PaymentStatus.PAID, 999m, paidAt: periodStart.AddDays(6)),
            CreatePayment(PaymentType.DEPOSIT, PaymentStatus.PAID, 999m, paidAt: periodEnd.AddDays(1)),
            CreatePayment(PaymentType.DEPOSIT, PaymentStatus.PAID, 999m),
            CreatePayment(PaymentType.DEPOSIT, PaymentStatus.PENDING, 50m, expiredAt: now.AddDays(1)),
            CreatePayment(PaymentType.REMAINING_PAYMENT, PaymentStatus.PROCESSING, 30m),
            CreatePayment(PaymentType.DEPOSIT, PaymentStatus.PROCESSING, 999m, expiredAt: now.AddDays(-1)),
            CreatePayment(PaymentType.DEPOSIT, PaymentStatus.PENDING, 999m, paymentId: successfulPendingPaymentId),
            CreatePayment(PaymentType.DEPOSIT, PaymentStatus.PAID, 999m, paidAt: periodStart.AddDays(1), currency: "USD"));
        context.PaymentTransactionSet.Add(CreateTransaction(
            PaymentTransactionStatus.SUCCESS,
            periodStart.AddDays(1),
            "VND",
            successfulPendingPaymentId));
    }

    private static void SeedOrders(AppDbContext context, DateTime periodStart, DateTime periodEnd)
    {
        context.OrderSet.AddRange(
            CreateOrder(OrderStatus.DEPOSIT_PENDING, 70m, 300m, confirmedAt: periodStart.AddDays(1)),
            CreateOrder(OrderStatus.IN_PRODUCTION, 200m, 600m, confirmedAt: periodStart.AddDays(2)),
            CreateOrder(OrderStatus.CANCELLED, 999m, 999m, confirmedAt: periodStart.AddDays(3)),
            CreateOrder(OrderStatus.COMPLETED, 999m, 999m, confirmedAt: periodStart.AddDays(4)),
            CreateOrder(OrderStatus.DEPOSIT_PAID, 999m, 999m, confirmedAt: periodEnd.AddDays(1)));
    }

    private static void SeedTransactions(AppDbContext context, DateTime periodStart)
    {
        context.PaymentTransactionSet.AddRange(
            CreateTransaction(PaymentTransactionStatus.FAILED, periodStart.AddDays(1)),
            CreateTransaction(PaymentTransactionStatus.SUCCESS, periodStart.AddDays(1)),
            CreateTransaction(PaymentTransactionStatus.FAILED, periodStart.AddDays(1), "USD"));
    }

    private static Payment CreatePayment(
        PaymentType type,
        PaymentStatus status,
        decimal amount,
        DateTime? paidAt = null,
        DateTime? expiredAt = null,
        string currency = "VND",
        Guid? paymentId = null,
        Guid? orderId = null,
        Guid? projectId = null,
        DateTime? createdAt = null)
    {
        return new Payment
        {
            PaymentId = paymentId ?? Guid.NewGuid(),
            ProjectId = projectId ?? Guid.NewGuid(),
            OrderId = orderId,
            PaymentCode = Guid.NewGuid().ToString("N")[..12],
            PaymentType = type,
            Status = status,
            Amount = amount,
            Currency = currency,
            PaidAt = paidAt,
            ExpiredAt = expiredAt,
            CreatedAt = createdAt ?? paidAt
        };
    }

    private static Order CreateOrder(
        OrderStatus status,
        decimal remainingAmount,
        decimal finalTotalAmount,
        DateTime? confirmedAt)
    {
        return CreateOrder(status, remainingAmount, finalTotalAmount, confirmedAt, Guid.NewGuid(), Guid.NewGuid(), null);
    }

    private static Order CreateOrder(
        OrderStatus status,
        decimal remainingAmount,
        decimal finalTotalAmount,
        DateTime? confirmedAt,
        Guid projectId,
        Guid customerId,
        Guid? salesId)
    {
        return new Order
        {
            OrderId = Guid.NewGuid(),
            ProjectId = projectId,
            QuotationId = Guid.NewGuid(),
            OrderCode = Guid.NewGuid().ToString("N")[..12],
            CustomerId = customerId,
            SalesId = salesId,
            OriginalTotalAmount = finalTotalAmount,
            FinalTotalAmount = finalTotalAmount,
            RemainingAmount = remainingAmount,
            Status = status,
            ConfirmedAt = confirmedAt,
            CreatedAt = confirmedAt
        };
    }

    private static Project CreateProject(
        Guid customerId,
        Guid? salesId,
        ProjectStatus status = ProjectStatus.SUBMITTED,
        DateTime? createdAt = null,
        string projectName = "Financial Project")
    {
        return new Project
        {
            ProjectId = Guid.NewGuid(),
            CustomerId = customerId,
            AssignedSalesId = salesId,
            ProjectCode = Guid.NewGuid().ToString("N")[..12],
            ProjectName = projectName,
            Status = status,
            CreatedAt = createdAt
        };
    }

    private static Account CreateAccount(Guid accountId, string fullName)
    {
        return new Account
        {
            AccountId = accountId,
            Email = $"{accountId:N}@example.test",
            FullName = fullName,
            PasswordHash = "hash",
            Status = AccountStatus.ACTIVE
        };
    }

    private static PaymentTransaction CreateTransaction(
        PaymentTransactionStatus status,
        DateTime createdAt,
        string currency = "VND",
        Guid? paymentId = null,
        PaymentProvider? provider = null,
        string? failureReason = null)
    {
        return new PaymentTransaction
        {
            PaymentTransactionId = Guid.NewGuid(),
            PaymentId = paymentId ?? Guid.NewGuid(),
            TransactionCode = Guid.NewGuid().ToString("N")[..12],
            TransactionType = PaymentTransactionType.CHARGE,
            Amount = 10m,
            Currency = currency,
            PaymentProvider = provider,
            Status = status,
            FailureReason = failureReason,
            CreatedAt = createdAt
        };
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static bool HasIndex<TEntity>(AppDbContext context, string databaseName, string? filter)
    {
        var entityType = context.Model.FindEntityType(typeof(TEntity));
        return entityType?.GetIndexes().Any(index =>
            index.GetDatabaseName() == databaseName &&
            index.GetFilter() == filter) == true;
    }
}
