using System;
using FurniSpace.Application.Common.Dashboard;
using FurniSpace.Domain.Enums;
using Xunit;

namespace FurniSpace.Application.Tests.Dashboard;

public sealed class DashboardNextActionResolverTests
{
    [Fact]
    public void ResolveSales_Submitted_ReturnsReviewRequest()
    {
        var projectId = Guid.NewGuid();

        var result = DashboardNextActionResolver.ResolveSales(
            ProjectStatus.SUBMITTED,
            projectId,
            orderId: null,
            orderStatus: null,
            remainingAmount: null,
            customerConfirmedDeliveryAt: null,
            dueBucket: null);

        Assert.Equal("Intake", result.Group);
        Assert.Equal("Review request", result.Action);
        Assert.Equal("HIGH", result.Priority);
        Assert.Contains(projectId.ToString("D"), result.ActionPath);
        Assert.Null(result.Warning);
    }

    [Fact]
    public void ResolveSales_FinalPaymentUnpaid_ReturnsCreateRemainingPayment()
    {
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var result = DashboardNextActionResolver.ResolveSales(
            ProjectStatus.DELIVERED,
            projectId,
            orderId,
            OrderStatus.FINAL_PAYMENT_PENDING,
            remainingAmount: 1_500_000m,
            customerConfirmedDeliveryAt: null,
            dueBucket: null);

        Assert.Equal("Order and Payment", result.Group);
        Assert.Equal("Create Remaining Payment", result.Action);
        Assert.Equal("Remaining unpaid", result.Warning);
        Assert.Contains(orderId.ToString("D"), result.ActionPath);
    }

    [Fact]
    public void ResolveSales_FinalPaymentPaidWithoutConfirm_ReturnsWaitingDeliveryConfirm()
    {
        var orderId = Guid.NewGuid();

        var result = DashboardNextActionResolver.ResolveSales(
            ProjectStatus.DELIVERED,
            Guid.NewGuid(),
            orderId,
            OrderStatus.FINAL_PAYMENT_PENDING,
            remainingAmount: 0m,
            customerConfirmedDeliveryAt: null,
            dueBucket: "OVERDUE");

        Assert.Equal("Waiting delivery confirm", result.Action);
        Assert.Equal("Waiting customer confirm", result.Warning);
        Assert.Equal("HIGH", result.Priority);
    }

    [Fact]
    public void ResolveDesigner_MeasurementRequired_ReturnsCompleteMeasurement()
    {
        var projectId = Guid.NewGuid();

        var result = DashboardNextActionResolver.ResolveDesigner(
            ProjectStatus.MEASUREMENT_REQUIRED,
            projectId,
            dueBucket: null);

        Assert.Equal("Design", result.Group);
        Assert.Equal("Complete measurement", result.Action);
        Assert.Equal("HIGH", result.Priority);
    }

    [Fact]
    public void ResolveProduction_PendingReview_ReturnsReviewAction()
    {
        var productionRequestId = Guid.NewGuid();

        var result = DashboardNextActionResolver.ResolveProduction(
            ProductionRequestStatus.PENDING_REVIEW,
            productionRequestId,
            Guid.NewGuid(),
            dueBucket: null);

        Assert.Equal("Production", result.Group);
        Assert.Equal("Review production request", result.Action);
        Assert.Contains(productionRequestId.ToString("D"), result.ActionPath);
    }

    [Fact]
    public void ResolveProduction_InProductionWithBlockedItems_ReturnsResolveBlocked()
    {
        var result = DashboardNextActionResolver.ResolveProduction(
            ProductionRequestStatus.IN_PRODUCTION,
            Guid.NewGuid(),
            Guid.NewGuid(),
            dueBucket: null,
            blockedItemCount: 2);

        Assert.Equal("Resolve blocked items", result.Action);
        Assert.Equal("Blocked or unavailable items", result.Warning);
    }
}

public sealed class DashboardDueHelperTests
{
    [Fact]
    public void ResolveDueBucket_PastDate_IsOverdue()
    {
        var now = new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);
        var dueAt = new DateTime(2026, 8, 10, 23, 59, 59, DateTimeKind.Utc);

        Assert.Equal("OVERDUE", DashboardDueHelper.ResolveDueBucket(dueAt, now));
    }

    [Fact]
    public void ResolveDueBucket_Today_IsToday()
    {
        var now = new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);
        var dueAt = new DateTime(2026, 8, 17, 23, 59, 59, DateTimeKind.Utc);

        Assert.Equal("TODAY", DashboardDueHelper.ResolveDueBucket(dueAt, now));
    }

    [Fact]
    public void ToDueAtUtc_Null_ReturnsNull()
    {
        Assert.Null(DashboardDueHelper.ToDueAtUtc(null));
    }
}
