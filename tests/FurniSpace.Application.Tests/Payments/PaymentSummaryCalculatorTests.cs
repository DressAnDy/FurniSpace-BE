using System;
using FurniSpace.Application.Common.Payments;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using Xunit;

namespace FurniSpace.Application.Tests.Payments;

public sealed class PaymentSummaryCalculatorTests
{
    [Fact]
    public void ApplyCharge_WhenFullyPaid_UpdatesStatusAndPaidAt()
    {
        var payment = CreatePayment(amount: 100m, paidAmount: 0m);
        var occurredAt = new DateTime(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc);

        PaymentSummaryCalculator.ApplyCharge(payment, 100m, occurredAt);

        Assert.Equal(PaymentStatus.PAID, payment.Status);
        Assert.Equal(100m, payment.PaidAmount);
        Assert.Equal(0m, payment.RemainingAmount);
        Assert.Equal(occurredAt, payment.PaidAt);
        Assert.Equal(occurredAt, payment.UpdatedAt);
    }

    [Fact]
    public void ApplyCharge_WhenPartiallyPaid_KeepsPaidAtNull()
    {
        var payment = CreatePayment(amount: 300m, paidAmount: 0m);
        var occurredAt = new DateTime(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc);

        PaymentSummaryCalculator.ApplyCharge(payment, 100m, occurredAt);

        Assert.Equal(PaymentStatus.PARTIALLY_PAID, payment.Status);
        Assert.Equal(100m, payment.PaidAmount);
        Assert.Equal(200m, payment.RemainingAmount);
        Assert.Null(payment.PaidAt);
    }

    private static Payment CreatePayment(decimal amount, decimal paidAmount)
    {
        return new Payment
        {
            PaymentId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            PaymentCode = "FS12345678",
            Amount = amount,
            PaidAmount = paidAmount,
            RemainingAmount = amount - paidAmount,
            Status = PaymentStatus.PENDING
        };
    }
}
