using System;
using FurniSpace.Application.Common.Payments;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using Xunit;

namespace FurniSpace.Application.Tests.Payments;

public sealed class ProjectStartFeeRulesTests
{
    [Fact]
    public void IsEligibleForDesignerAssignment_ReturnsTrue_WhenProjectStartFeeIsPaid()
    {
        var payment = new Payment
        {
            PaymentType = PaymentType.PROJECT_START_FEE,
            Status = PaymentStatus.PAID
        };

        Assert.True(ProjectStartFeeRules.IsEligibleForDesignerAssignment(payment));
    }

    [Fact]
    public void IsEligibleForDesignerAssignment_ReturnsFalse_WhenProjectStartFeeIsPending()
    {
        var payment = new Payment
        {
            PaymentType = PaymentType.PROJECT_START_FEE,
            Status = PaymentStatus.PENDING
        };

        Assert.False(ProjectStartFeeRules.IsEligibleForDesignerAssignment(payment));
    }

    [Fact]
    public void BuildStatus_ReflectsPaymentState()
    {
        var projectId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var payment = new Payment
        {
            PaymentId = paymentId,
            Status = PaymentStatus.PAID
        };

        var status = ProjectStartFeeRules.BuildStatus(projectId, payment);

        Assert.Equal(projectId, status.ProjectId);
        Assert.True(status.RequiresProjectStartFee);
        Assert.True(status.IsEligibleForDesignerAssignment);
        Assert.Equal(PaymentStatus.PAID, status.ProjectStartFeeStatus);
        Assert.Equal(paymentId, status.PaymentId);
    }
}
