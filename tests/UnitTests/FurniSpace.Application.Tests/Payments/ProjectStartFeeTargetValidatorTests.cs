using System;
using FurniSpace.Application.Common.Payments;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using Xunit;

namespace FurniSpace.Application.Tests.Payments;

public sealed class ProjectStartFeeTargetValidatorTests
{
    [Fact]
    public void ValidateCreateExpiry_WhenExpiryInPast_ReturnsPaymentExpired()
    {
        var utcNow = new DateTime(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);

        var error = ProjectStartFeeTargetValidator.ValidateCreateExpiry(
            utcNow.AddMinutes(-1),
            new DateOnly(2026, 12, 31),
            utcNow);

        Assert.NotNull(error);
        Assert.Equal(PaymentErrorCodes.PaymentExpired, error!.Code);
    }

    [Fact]
    public void ValidateCreateExpiry_WhenExpiryExceedsTarget_ReturnsValidationError()
    {
        var utcNow = new DateTime(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);

        var error = ProjectStartFeeTargetValidator.ValidateCreateExpiry(
            utcNow.AddDays(20),
            new DateOnly(2026, 8, 10),
            utcNow);

        Assert.NotNull(error);
        Assert.Equal(PaymentErrorCodes.ProjectStartFeeExpiryExceedsTarget, error!.Code);
    }

    [Fact]
    public void ValidateTargetUpdateAgainstActiveStartFee_WhenExpiryAfterNewTarget_ReturnsConflict()
    {
        var utcNow = new DateTime(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);
        var payment = new Payment
        {
            Status = PaymentStatus.PENDING,
            ExpiredAt = utcNow.AddDays(30)
        };

        var error = ProjectStartFeeTargetValidator.ValidateTargetUpdateAgainstActiveStartFee(
            new DateOnly(2026, 8, 15),
            payment,
            utcNow);

        Assert.NotNull(error);
        Assert.Equal(ProjectErrorCodes.TargetDateConflictsWithActiveStartFee, error!.Code);
    }

    [Fact]
    public void ValidateTargetUpdateAgainstActiveStartFee_WhenPaymentPaid_DoesNotBlock()
    {
        var utcNow = new DateTime(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);
        var payment = new Payment
        {
            Status = PaymentStatus.PAID,
            ExpiredAt = utcNow.AddDays(30)
        };

        var error = ProjectStartFeeTargetValidator.ValidateTargetUpdateAgainstActiveStartFee(
            new DateOnly(2026, 8, 1),
            payment,
            utcNow);

        Assert.Null(error);
    }
}
