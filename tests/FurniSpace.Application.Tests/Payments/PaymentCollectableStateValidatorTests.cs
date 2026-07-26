using System;
using FurniSpace.Application.Common.Payments;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using Xunit;

namespace FurniSpace.Application.Tests.Payments;

public sealed class PaymentCollectableStateValidatorTests
{
    [Fact]
    public void Validate_WithPendingPayment_ReturnsValid()
    {
        var payment = CreatePayment(PaymentStatus.PENDING, amount: 100m);

        var result = PaymentCollectableStateValidator.Validate(payment);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(PaymentStatus.PAID)]
    [InlineData(PaymentStatus.CANCELLED)]
    [InlineData(PaymentStatus.EXPIRED)]
    [InlineData(PaymentStatus.REFUNDED)]
    public void Validate_WithNonCollectableStatus_ReturnsInvalidStatus(PaymentStatus status)
    {
        var payment = CreatePayment(status, amount: 100m);

        var result = PaymentCollectableStateValidator.Validate(payment);

        Assert.False(result.IsValid);
        Assert.Equal(PaymentErrorCodes.PaymentNotPayable, result.ErrorCode);
    }

    [Fact]
    public void Validate_WithExpiredPayment_ReturnsPaymentExpired()
    {
        var payment = CreatePayment(PaymentStatus.PENDING, amount: 100m);
        payment.ExpiredAt = DateTime.UtcNow.AddMinutes(-5);

        var result = PaymentCollectableStateValidator.Validate(payment);

        Assert.False(result.IsValid);
        Assert.Equal(PaymentErrorCodes.PaymentExpired, result.ErrorCode);
    }

    [Fact]
    public void Validate_WithZeroAmount_ReturnsInvalidAmount()
    {
        var payment = CreatePayment(PaymentStatus.PENDING, amount: 0m);

        var result = PaymentCollectableStateValidator.Validate(payment);

        Assert.False(result.IsValid);
        Assert.Equal(PaymentErrorCodes.InvalidPaymentAmount, result.ErrorCode);
    }

    private static Payment CreatePayment(PaymentStatus status, decimal amount)
    {
        return new Payment
        {
            PaymentId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            PaymentCode = "FS12345678",
            Amount = amount,
            Status = status
        };
    }
}
