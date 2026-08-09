#nullable enable

using System;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
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
        Guid? paymentId = null)
    {
        return new Payment
        {
            PaymentId = paymentId ?? Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            PaymentCode = Guid.NewGuid().ToString("N")[..12],
            PaymentType = type,
            Status = status,
            Amount = amount,
            Currency = currency,
            PaidAt = paidAt,
            ExpiredAt = expiredAt,
            CreatedAt = paidAt
        };
    }

    private static Order CreateOrder(
        OrderStatus status,
        decimal remainingAmount,
        decimal finalTotalAmount,
        DateTime? confirmedAt)
    {
        return new Order
        {
            OrderId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            QuotationId = Guid.NewGuid(),
            OrderCode = Guid.NewGuid().ToString("N")[..12],
            CustomerId = Guid.NewGuid(),
            OriginalTotalAmount = finalTotalAmount,
            FinalTotalAmount = finalTotalAmount,
            RemainingAmount = remainingAmount,
            Status = status,
            ConfirmedAt = confirmedAt,
            CreatedAt = confirmedAt
        };
    }

    private static PaymentTransaction CreateTransaction(
        PaymentTransactionStatus status,
        DateTime createdAt,
        string currency = "VND",
        Guid? paymentId = null)
    {
        return new PaymentTransaction
        {
            PaymentTransactionId = Guid.NewGuid(),
            PaymentId = paymentId ?? Guid.NewGuid(),
            TransactionCode = Guid.NewGuid().ToString("N")[..12],
            TransactionType = PaymentTransactionType.CHARGE,
            Amount = 10m,
            Currency = currency,
            Status = status,
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
}
