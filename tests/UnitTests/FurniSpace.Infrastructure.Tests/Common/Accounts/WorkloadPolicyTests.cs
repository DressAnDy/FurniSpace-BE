using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Accounts;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Common.Accounts;

public sealed class WorkloadPolicyTests
{
    [Theory]
    [InlineData(0, 2, DesignerWorkloadStatusSets.CapacityAvailable)]
    [InlineData(1, 2, DesignerWorkloadStatusSets.CapacityAvailable)]
    [InlineData(2, 2, DesignerWorkloadStatusSets.CapacityFull)]
    [InlineData(3, 2, DesignerWorkloadStatusSets.CapacityOver)]
    public void Designer_ResolveCapacityState(int designActive, int max, string expected)
    {
        Assert.Equal(expected, DesignerWorkloadStatusSets.ResolveCapacityState(designActive, max));
    }

    [Theory]
    [InlineData(ProjectStatus.MEASUREMENT_REQUIRED, DesignerWorkloadStatusSets.BucketDesignActive)]
    [InlineData(ProjectStatus.SPACE_VERIFIED, DesignerWorkloadStatusSets.BucketDesignActive)]
    [InlineData(ProjectStatus.PROPOSAL_CONSULTING, DesignerWorkloadStatusSets.BucketDesignActive)]
    [InlineData(ProjectStatus.ORDER_CONFIRMED, DesignerWorkloadStatusSets.BucketPostDesign)]
    [InlineData(ProjectStatus.DELIVERED, DesignerWorkloadStatusSets.BucketPostDesign)]
    [InlineData(ProjectStatus.COMPLETED, DesignerWorkloadStatusSets.BucketTerminal)]
    [InlineData(ProjectStatus.REJECTED, DesignerWorkloadStatusSets.BucketTerminal)]
    [InlineData(ProjectStatus.SUBMITTED, DesignerWorkloadStatusSets.BucketOther)]
    [InlineData(null, DesignerWorkloadStatusSets.BucketOther)]
    public void Designer_ResolveBucket(ProjectStatus? status, string expected)
    {
        Assert.Equal(expected, DesignerWorkloadStatusSets.ResolveBucket(status));
    }

    [Theory]
    [InlineData(0, 5, SalesWorkloadPressurePolicy.CapacityAvailableNow)]
    [InlineData(4, 5, SalesWorkloadPressurePolicy.CapacityAvailableNow)]
    [InlineData(5, 5, SalesWorkloadPressurePolicy.CapacityFullNow)]
    [InlineData(6, 5, SalesWorkloadPressurePolicy.CapacityOverNow)]
    public void Sales_ResolveCapacityState(int salesActive, int max, string expected)
    {
        Assert.Equal(expected, SalesWorkloadPressurePolicy.ResolveCapacityState(salesActive, max));
    }

    [Theory]
    [InlineData(0, SalesWorkloadPressurePolicy.PressureLow)]
    [InlineData(1.49, SalesWorkloadPressurePolicy.PressureLow)]
    [InlineData(1.5, SalesWorkloadPressurePolicy.PressureMedium)]
    [InlineData(2.99, SalesWorkloadPressurePolicy.PressureMedium)]
    [InlineData(3.0, SalesWorkloadPressurePolicy.PressureHigh)]
    [InlineData(10, SalesWorkloadPressurePolicy.PressureHigh)]
    public void Sales_ResolveFuturePressureState(decimal score, string expected)
    {
        Assert.Equal(expected, SalesWorkloadPressurePolicy.ResolveFuturePressureState(score));
    }

    [Fact]
    public void Sales_ComputeFuturePressureScore_SumsWeightedCounts()
    {
        var score = SalesWorkloadPressurePolicy.ComputeFuturePressureScore(
            measurementRequiredCount: 1,
            spaceVerifiedCount: 1,
            proposalConsultingCount: 1,
            inProductionCount: 1,
            productionBlockedCount: 1,
            readyForDeliveryCount: 1,
            deliveringCount: 1,
            deliveredCount: 1);

        Assert.Equal(
            0.25m + 0.50m + 1.00m + 0.20m + 0.75m + 0.30m + 0.40m + 0.50m,
            score);
    }

    [Theory]
    [InlineData(ProjectStatus.MEASUREMENT_REQUIRED, 0.25)]
    [InlineData(ProjectStatus.SPACE_VERIFIED, 0.50)]
    [InlineData(ProjectStatus.PROPOSAL_CONSULTING, 1.00)]
    [InlineData(ProjectStatus.IN_PRODUCTION, 0.20)]
    [InlineData(ProjectStatus.PRODUCTION_BLOCKED, 0.75)]
    [InlineData(ProjectStatus.READY_FOR_DELIVERY, 0.30)]
    [InlineData(ProjectStatus.DELIVERING, 0.40)]
    [InlineData(ProjectStatus.DELIVERED, 0.50)]
    [InlineData(ProjectStatus.IN_CONSULTATION, 0)]
    [InlineData(null, 0)]
    public void Sales_ResolvePressureWeight(ProjectStatus? status, double expected)
    {
        Assert.Equal((decimal)expected, SalesWorkloadPressurePolicy.ResolvePressureWeight(status));
    }

    [Theory]
    [InlineData(ProjectStatus.IN_CONSULTATION, SalesWorkloadPressurePolicy.BucketIntake)]
    [InlineData(ProjectStatus.QUOTATION_SENT, SalesWorkloadPressurePolicy.BucketCommercial)]
    [InlineData(ProjectStatus.PROPOSAL_CONSULTING, SalesWorkloadPressurePolicy.BucketDesignMonitor)]
    [InlineData(ProjectStatus.PRODUCTION_BLOCKED, SalesWorkloadPressurePolicy.BucketFulfillment)]
    [InlineData(ProjectStatus.COMPLETED, SalesWorkloadPressurePolicy.BucketTerminal)]
    [InlineData(ProjectStatus.SUBMITTED, SalesWorkloadPressurePolicy.BucketOther)]
    [InlineData(null, SalesWorkloadPressurePolicy.BucketOther)]
    public void Sales_ResolveBucket(ProjectStatus? status, string expected)
    {
        Assert.Equal(expected, SalesWorkloadPressurePolicy.ResolveBucket(status));
    }
}
