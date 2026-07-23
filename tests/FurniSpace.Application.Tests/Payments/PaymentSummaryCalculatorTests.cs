using System;
using FurniSpace.Application.Common.Payments;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using Xunit;

namespace FurniSpace.Application.Tests.Payments;

public sealed class PaymentSummaryCalculatorTests
{
    [Fact]
    public void TryApplySuccessfulCharge_WhenAmountMatches_SetsPaid()
    {
        var payment = CreatePayment(100m);
        var occurredAt = new DateTime(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc);

        var applied = PaymentSummaryCalculator.TryApplySuccessfulCharge(
            payment,
            100m,
            "VND",
            occurredAt,
            out var errorCode);

        Assert.True(applied);
        Assert.Null(errorCode);
        Assert.Equal(PaymentStatus.PAID, payment.Status);
        Assert.Equal(occurredAt, payment.PaidAt);
        Assert.Equal(occurredAt, payment.UpdatedAt);
    }

    [Fact]
    public void TryApplySuccessfulCharge_WhenAmountMismatch_ReturnsError()
    {
        var payment = CreatePayment(100m);
        var occurredAt = new DateTime(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc);

        var applied = PaymentSummaryCalculator.TryApplySuccessfulCharge(
            payment,
            50m,
            "VND",
            occurredAt,
            out var errorCode);

        Assert.False(applied);
        Assert.Equal(PaymentErrorCodes.PaymentAmountMismatch, errorCode);
        Assert.Equal(PaymentStatus.PENDING, payment.Status);
    }

    private static Payment CreatePayment(decimal amount)
    {
        return new Payment
        {
            PaymentId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            PaymentCode = "FS12345678",
            Amount = amount,
            Currency = "VND",
            Status = PaymentStatus.PENDING
        };
    }
}
