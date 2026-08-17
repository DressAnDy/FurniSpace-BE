using System;
using FurniSpace.Application.Common.Dashboard;
using FurniSpace.Domain.Enums;
using Xunit;

namespace FurniSpace.Application.Tests.Dashboard;

public sealed class DashboardNextActionResolverTests
{
    [Theory]
    [InlineData(ProjectStatus.SUBMITTED, "Review request", "Intake")]
    [InlineData(ProjectStatus.IN_CONSULTATION, "Continue consultation", "Intake")]
    [InlineData(ProjectStatus.NEED_BASIC_INFORMATION, "Waiting customer info", "Intake")]
    [InlineData(ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT, "Assign designer", "Intake")]
    [InlineData(ProjectStatus.MEASUREMENT_REQUIRED, "Follow design progress", "Design")]
    [InlineData(ProjectStatus.SPACE_VERIFIED, "Follow design progress", "Design")]
    [InlineData(ProjectStatus.PROPOSAL_CONSULTING, "Manage proposal", "Proposal and Quotation")]
    [InlineData(ProjectStatus.PROPOSAL_SELECTED, "Manage proposal", "Proposal and Quotation")]
    [InlineData(ProjectStatus.QUOTATION_SENT, "Waiting quotation accept", "Proposal and Quotation")]
    [InlineData(ProjectStatus.QUOTATION_REVISION_REQUESTED, "Revise quotation", "Proposal and Quotation")]
    [InlineData(ProjectStatus.ORDER_CONFIRMED, "Monitor order", "Order and Payment")]
    [InlineData(ProjectStatus.IN_PRODUCTION, "Monitor order", "Order and Payment")]
    [InlineData(ProjectStatus.READY_FOR_DELIVERY, "Monitor delivery", "Delivery")]
    [InlineData(ProjectStatus.DELIVERING, "Monitor delivery", "Delivery")]
    [InlineData(ProjectStatus.DELIVERED, "Monitor delivery", "Delivery")]
    [InlineData(ProjectStatus.COMPLETED, "Complete", "Delivery")]
    [InlineData(ProjectStatus.REJECTED, "Review rejected request", "Intake")]
    public void ResolveSales_ByProjectStatus_ReturnsExpectedAction(
        ProjectStatus status,
        string expectedAction,
        string expectedGroup)
    {
        var projectId = Guid.NewGuid();

        var result = DashboardNextActionResolver.ResolveSales(
            status,
            projectId,
            orderId: null,
            orderStatus: null,
            remainingAmount: null,
            customerConfirmedDeliveryAt: null,
            dueBucket: null);

        Assert.Equal(expectedAction, result.Action);
        Assert.Equal(expectedGroup, result.Group);
    }

    [Fact]
    public void ResolveSales_NullStatus_ReturnsFallback()
    {
        var result = DashboardNextActionResolver.ResolveSales(
            null,
            Guid.NewGuid(),
            null,
            null,
            null,
            null,
            null);

        Assert.Equal("Review project", result.Action);
        Assert.Equal("UNKNOWN", result.Phase);
    }

    [Fact]
    public void ResolveSales_DepositPending_ReturnsFollowUpDeposit()
    {
        var orderId = Guid.NewGuid();

        var result = DashboardNextActionResolver.ResolveSales(
            ProjectStatus.ORDER_CONFIRMED,
            Guid.NewGuid(),
            orderId,
            OrderStatus.DEPOSIT_PENDING,
            remainingAmount: 100m,
            customerConfirmedDeliveryAt: null,
            dueBucket: null);

        Assert.Equal("Follow up deposit", result.Action);
        Assert.Equal("Payment follow-up", result.Warning);
        Assert.Contains(orderId.ToString("D"), result.ActionPath);
    }

    [Fact]
    public void ResolveSales_FinalPaymentUnpaid_ReturnsCreateRemainingPayment()
    {
        var orderId = Guid.NewGuid();

        var result = DashboardNextActionResolver.ResolveSales(
            ProjectStatus.DELIVERED,
            Guid.NewGuid(),
            orderId,
            OrderStatus.FINAL_PAYMENT_PENDING,
            remainingAmount: 1_500_000m,
            customerConfirmedDeliveryAt: null,
            dueBucket: null);

        Assert.Equal("Create Remaining Payment", result.Action);
        Assert.Equal("Remaining unpaid", result.Warning);
    }

    [Fact]
    public void ResolveSales_FinalPaymentPaidWithoutConfirm_ReturnsWaitingDeliveryConfirm()
    {
        var result = DashboardNextActionResolver.ResolveSales(
            ProjectStatus.DELIVERED,
            Guid.NewGuid(),
            Guid.NewGuid(),
            OrderStatus.FINAL_PAYMENT_PENDING,
            remainingAmount: 0m,
            customerConfirmedDeliveryAt: null,
            dueBucket: "OVERDUE");

        Assert.Equal("Waiting delivery confirm", result.Action);
        Assert.Equal("Waiting customer confirm", result.Warning);
        Assert.Equal("HIGH", result.Priority);
    }

    [Fact]
    public void ResolveSales_FinalPaymentPaidAndConfirmed_FallsThroughToProjectStatus()
    {
        var result = DashboardNextActionResolver.ResolveSales(
            ProjectStatus.DELIVERED,
            Guid.NewGuid(),
            Guid.NewGuid(),
            OrderStatus.FINAL_PAYMENT_PENDING,
            remainingAmount: 0m,
            customerConfirmedDeliveryAt: DateTime.UtcNow,
            dueBucket: null);

        Assert.Equal("Monitor delivery", result.Action);
    }

    [Fact]
    public void ResolveSales_OrderConfirmedWithOrderId_UsesOrderPath()
    {
        var orderId = Guid.NewGuid();

        var result = DashboardNextActionResolver.ResolveSales(
            ProjectStatus.ORDER_CONFIRMED,
            Guid.NewGuid(),
            orderId,
            OrderStatus.DEPOSIT_PAID,
            null,
            null,
            null);

        Assert.Equal("Monitor order", result.Action);
        Assert.Contains(orderId.ToString("D"), result.ActionPath);
    }

    [Theory]
    [InlineData(ProjectStatus.MEASUREMENT_REQUIRED, "Complete measurement")]
    [InlineData(ProjectStatus.SPACE_VERIFIED, "Start proposal")]
    [InlineData(ProjectStatus.PROPOSAL_CONSULTING, "Manage proposals")]
    [InlineData(ProjectStatus.PROPOSAL_SELECTED, "Proposal selected")]
    [InlineData(ProjectStatus.QUOTATION_REVISION_REQUESTED, "Support quotation revision")]
    [InlineData(ProjectStatus.SUBMITTED, "Review design work")]
    public void ResolveDesigner_ByStatus_ReturnsExpectedAction(ProjectStatus status, string expectedAction)
    {
        var result = DashboardNextActionResolver.ResolveDesigner(status, Guid.NewGuid(), null);
        Assert.Equal(expectedAction, result.Action);
    }

    [Fact]
    public void ResolveDesigner_NullStatus_ReturnsFallback()
    {
        var result = DashboardNextActionResolver.ResolveDesigner(null, Guid.NewGuid(), "OVERDUE");
        Assert.Equal("Review design work", result.Action);
        Assert.Equal("HIGH", result.Priority);
    }

    [Theory]
    [InlineData(ProductionRequestStatus.PENDING_REVIEW, "Review production request")]
    [InlineData(ProductionRequestStatus.FEASIBLE, "Start production")]
    [InlineData(ProductionRequestStatus.IN_PRODUCTION, "Continue / complete production")]
    [InlineData(ProductionRequestStatus.COMPLETED, "Completed")]
    [InlineData(ProductionRequestStatus.CANCELLED, "Cancelled")]
    public void ResolveProduction_ByStatus_ReturnsExpectedAction(
        ProductionRequestStatus status,
        string expectedAction)
    {
        var result = DashboardNextActionResolver.ResolveProduction(
            status,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null);

        Assert.Equal(expectedAction, result.Action);
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
    private static readonly DateTime Now = new(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ToDueAtUtc_WithDate_ReturnsUtcEndOfDay()
    {
        var dueAt = DashboardDueHelper.ToDueAtUtc(new DateOnly(2026, 8, 20));

        Assert.NotNull(dueAt);
        Assert.Equal(DateTimeKind.Utc, dueAt.Value.Kind);
        Assert.Equal(new DateOnly(2026, 8, 20), DateOnly.FromDateTime(dueAt.Value));
    }

    [Fact]
    public void ToDueAtUtc_Null_ReturnsNull()
    {
        Assert.Null(DashboardDueHelper.ToDueAtUtc(null));
    }

    [Fact]
    public void ResolveDueBucket_Null_ReturnsNull()
    {
        Assert.Null(DashboardDueHelper.ResolveDueBucket(null, Now));
    }

    [Theory]
    [InlineData(2026, 8, 10, "OVERDUE")]
    [InlineData(2026, 8, 17, "TODAY")]
    [InlineData(2026, 8, 20, "THIS_WEEK")]
    [InlineData(2026, 9, 1, "LATER")]
    public void ResolveDueBucket_ReturnsExpectedBucket(int year, int month, int day, string expected)
    {
        var dueAt = new DateTime(year, month, day, 23, 59, 59, DateTimeKind.Utc);
        Assert.Equal(expected, DashboardDueHelper.ResolveDueBucket(dueAt, Now));
    }

    [Fact]
    public void MatchesDateRange_BlankOrUnknown_ReturnsTrue()
    {
        var dueAt = new DateTime(2026, 8, 17, 23, 59, 59, DateTimeKind.Utc);
        Assert.True(DashboardDueHelper.MatchesDateRange(dueAt, null, Now));
        Assert.True(DashboardDueHelper.MatchesDateRange(dueAt, " ", Now));
        Assert.True(DashboardDueHelper.MatchesDateRange(dueAt, "custom", Now));
    }

    [Fact]
    public void MatchesDateRange_NullDue_ReturnsTrue()
    {
        Assert.True(DashboardDueHelper.MatchesDateRange(null, "today", Now));
    }

    [Theory]
    [InlineData("today", 2026, 8, 17, true)]
    [InlineData("today", 2026, 8, 18, false)]
    [InlineData("thisWeek", 2026, 8, 16, true)]
    [InlineData("thisWeek", 2026, 8, 25, false)]
    [InlineData("thisMonth", 2026, 8, 31, true)]
    [InlineData("thisMonth", 2026, 9, 1, false)]
    public void MatchesDateRange_FiltersByRange(
        string range,
        int year,
        int month,
        int day,
        bool expected)
    {
        var dueAt = new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expected, DashboardDueHelper.MatchesDateRange(dueAt, range, Now));
    }
}
