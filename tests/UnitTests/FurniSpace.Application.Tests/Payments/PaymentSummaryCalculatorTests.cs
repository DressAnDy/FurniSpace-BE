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

    [Fact]
    public void TryApplySuccessfulCharge_WhenCurrencyMismatch_ReturnsError()
    {
        var payment = CreatePayment(100m);
        var occurredAt = new DateTime(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc);

        var applied = PaymentSummaryCalculator.TryApplySuccessfulCharge(
            payment,
            100m,
            "USD",
            occurredAt,
            out var errorCode);

        Assert.False(applied);
        Assert.Equal(PaymentErrorCodes.PaymentCurrencyMismatch, errorCode);
    }

    [Fact]
    public void TryApplySuccessfulCharge_WhenAlreadyPaid_ReturnsError()
    {
        var payment = CreatePayment(100m);
        payment.Status = PaymentStatus.PAID;

        var applied = PaymentSummaryCalculator.TryApplySuccessfulCharge(
            payment,
            100m,
            "VND",
            DateTime.UtcNow,
            out var errorCode);

        Assert.False(applied);
        Assert.Equal(PaymentErrorCodes.PaymentAlreadyPaid, errorCode);
    }

    [Fact]
    public void MarkProcessing_UpdatesPendingPaymentToProcessing()
    {
        var payment = CreatePayment(100m);
        var occurredAt = new DateTime(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc);

        PaymentSummaryCalculator.MarkProcessing(payment, occurredAt);

        Assert.Equal(PaymentStatus.PROCESSING, payment.Status);
        Assert.Equal(occurredAt, payment.UpdatedAt);
    }

    [Fact]
    public void RevertToPendingIfCollectable_RevertsProcessingPayment()
    {
        var payment = CreatePayment(100m);
        payment.Status = PaymentStatus.PROCESSING;
        payment.ExpiredAt = DateTime.UtcNow.AddHours(1);
        var occurredAt = new DateTime(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc);

        PaymentSummaryCalculator.RevertToPendingIfCollectable(payment, occurredAt);

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
