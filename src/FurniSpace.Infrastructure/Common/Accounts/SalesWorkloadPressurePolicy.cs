using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.Common.Accounts;

/// <summary>
/// Sales workload buckets, capacity, and future-pressure policy (SCRUM-414).
/// Derived at runtime — not persisted.
/// </summary>
public static class SalesWorkloadPressurePolicy
{
    public const int MaxActiveSalesProjects = 5;

    public static readonly ProjectStatus[] IntakeActive =
    [
        ProjectStatus.IN_CONSULTATION,
        ProjectStatus.NEED_BASIC_INFORMATION,
        ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT
    ];

    public static readonly ProjectStatus[] CommercialActive =
    [
        ProjectStatus.PROPOSAL_SELECTED,
        ProjectStatus.QUOTATION_SENT,
        ProjectStatus.QUOTATION_REVISION_REQUESTED,
        ProjectStatus.ORDER_CONFIRMED
    ];

    public static readonly ProjectStatus[] DesignMonitor =
    [
        ProjectStatus.MEASUREMENT_REQUIRED,
        ProjectStatus.SPACE_VERIFIED,
        ProjectStatus.PROPOSAL_CONSULTING
    ];

    public static readonly ProjectStatus[] Fulfillment =
    [
        ProjectStatus.IN_PRODUCTION,
        ProjectStatus.READY_FOR_DELIVERY,
        ProjectStatus.DELIVERING,
        ProjectStatus.DELIVERED
    ];

    public static readonly ProjectStatus[] Terminal =
    [
        ProjectStatus.COMPLETED,
        ProjectStatus.REJECTED
    ];

    public static readonly ProjectStatus[] LifecycleAssigned =
    [
        ProjectStatus.SUBMITTED,
        ProjectStatus.IN_CONSULTATION,
        ProjectStatus.NEED_BASIC_INFORMATION,
        ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT,
        ProjectStatus.MEASUREMENT_REQUIRED,
        ProjectStatus.SPACE_VERIFIED,
        ProjectStatus.PROPOSAL_CONSULTING,
        ProjectStatus.PROPOSAL_SELECTED,
        ProjectStatus.QUOTATION_SENT,
        ProjectStatus.QUOTATION_REVISION_REQUESTED,
        ProjectStatus.ORDER_CONFIRMED,
        ProjectStatus.IN_PRODUCTION,
        ProjectStatus.READY_FOR_DELIVERY,
        ProjectStatus.DELIVERING,
        ProjectStatus.DELIVERED
    ];

    public const decimal WeightMeasurementRequired = 0.25m;
    public const decimal WeightSpaceVerified = 0.50m;
    public const decimal WeightProposalConsulting = 1.00m;
    public const decimal WeightInProduction = 0.20m;
    public const decimal WeightReadyForDelivery = 0.30m;
    public const decimal WeightDelivering = 0.40m;
    public const decimal WeightDelivered = 0.50m;

    public const decimal PressureLowMaxExclusive = 1.5m;
    public const decimal PressureMediumMaxExclusive = 3.0m;

    public const string BucketIntake = "INTAKE";
    public const string BucketCommercial = "COMMERCIAL";
    public const string BucketDesignMonitor = "DESIGN_MONITOR";
    public const string BucketFulfillment = "FULFILLMENT";
    public const string BucketTerminal = "TERMINAL";
    public const string BucketOther = "OTHER";
    public const string BucketCurrentActive = "CURRENT_ACTIVE";

    public const string CapacityAvailableNow = "AVAILABLE_NOW";
    public const string CapacityFullNow = "FULL_NOW";
    public const string CapacityOverNow = "OVER_NOW";

    public const string PressureLow = "LOW";
    public const string PressureMedium = "MEDIUM";
    public const string PressureHigh = "HIGH";

    public static string ResolveCapacityState(int salesActiveCount, int maxActiveProjects)
    {
        if (salesActiveCount < maxActiveProjects)
        {
            return CapacityAvailableNow;
        }

        if (salesActiveCount == maxActiveProjects)
        {
            return CapacityFullNow;
        }

        return CapacityOverNow;
    }

    public static string ResolveFuturePressureState(decimal score)
    {
        if (score < PressureLowMaxExclusive)
        {
            return PressureLow;
        }

        if (score < PressureMediumMaxExclusive)
        {
            return PressureMedium;
        }

        return PressureHigh;
    }

    public static decimal ResolvePressureWeight(ProjectStatus? status)
    {
        if (!status.HasValue)
        {
            return 0m;
        }

        return status.Value switch
        {
            ProjectStatus.MEASUREMENT_REQUIRED => WeightMeasurementRequired,
            ProjectStatus.SPACE_VERIFIED => WeightSpaceVerified,
            ProjectStatus.PROPOSAL_CONSULTING => WeightProposalConsulting,
            ProjectStatus.IN_PRODUCTION => WeightInProduction,
            ProjectStatus.READY_FOR_DELIVERY => WeightReadyForDelivery,
            ProjectStatus.DELIVERING => WeightDelivering,
            ProjectStatus.DELIVERED => WeightDelivered,
            _ => 0m
        };
    }

    public static decimal ComputeFuturePressureScore(SalesFuturePressureCounts counts)
    {
        return
            counts.MeasurementRequiredCount * WeightMeasurementRequired +
            counts.SpaceVerifiedCount * WeightSpaceVerified +
            counts.ProposalConsultingCount * WeightProposalConsulting +
            counts.InProductionCount * WeightInProduction +
            counts.ReadyForDeliveryCount * WeightReadyForDelivery +
            counts.DeliveringCount * WeightDelivering +
            counts.DeliveredCount * WeightDelivered;
    }

    public static string ResolveBucket(ProjectStatus? status)
    {
        if (!status.HasValue)
        {
            return BucketOther;
        }

        if (IntakeActive.Contains(status.Value))
        {
            return BucketIntake;
        }

        if (CommercialActive.Contains(status.Value))
        {
            return BucketCommercial;
        }

        if (DesignMonitor.Contains(status.Value))
        {
            return BucketDesignMonitor;
        }

        if (Fulfillment.Contains(status.Value))
        {
            return BucketFulfillment;
        }

        if (Terminal.Contains(status.Value))
        {
            return BucketTerminal;
        }

        return BucketOther;
    }
}
