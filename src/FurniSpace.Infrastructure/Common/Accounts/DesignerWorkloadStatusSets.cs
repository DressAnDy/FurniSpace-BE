using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.Common.Accounts;

/// <summary>
/// Status buckets for Designer workload / capacity (SCRUM-412).
/// Capacity slot uses DESIGN_ACTIVE only; lifecycle covers non-terminal ownership.
/// </summary>
public static class DesignerWorkloadStatusSets
{
    public static readonly ProjectStatus[] DesignActive =
    [
        ProjectStatus.MEASUREMENT_REQUIRED,
        ProjectStatus.SPACE_VERIFIED,
        ProjectStatus.PROPOSAL_CONSULTING
    ];

    public static readonly ProjectStatus[] PostDesign =
    [
        ProjectStatus.PROPOSAL_SELECTED,
        ProjectStatus.QUOTATION_SENT,
        ProjectStatus.QUOTATION_REVISION_REQUESTED,
        ProjectStatus.ORDER_CONFIRMED,
        ProjectStatus.IN_PRODUCTION,
        ProjectStatus.PRODUCTION_BLOCKED,
        ProjectStatus.READY_FOR_DELIVERY,
        ProjectStatus.DELIVERING,
        ProjectStatus.DELIVERED
    ];

    public static readonly ProjectStatus[] Terminal =
    [
        ProjectStatus.COMPLETED,
        ProjectStatus.REJECTED
    ];

    /// <summary>
    /// Non-terminal statuses while designer remains assigned (design + post-design + edge).
    /// </summary>
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
        ProjectStatus.PRODUCTION_BLOCKED,
        ProjectStatus.READY_FOR_DELIVERY,
        ProjectStatus.DELIVERING,
        ProjectStatus.DELIVERED
    ];

    public const string BucketDesignActive = "DESIGN_ACTIVE";
    public const string BucketPostDesign = "POST_DESIGN";
    public const string BucketTerminal = "TERMINAL";
    public const string BucketOther = "OTHER";

    public const string CapacityAvailable = "AVAILABLE";
    public const string CapacityFull = "FULL";
    public const string CapacityOver = "OVER";

    public static string ResolveCapacityState(int designActiveCount, int maxActiveProjects)
    {
        if (designActiveCount < maxActiveProjects)
        {
            return CapacityAvailable;
        }

        if (designActiveCount == maxActiveProjects)
        {
            return CapacityFull;
        }

        return CapacityOver;
    }

    public static string ResolveBucket(ProjectStatus? status)
    {
        if (!status.HasValue)
        {
            return BucketOther;
        }

        if (DesignActive.Contains(status.Value))
        {
            return BucketDesignActive;
        }

        if (PostDesign.Contains(status.Value))
        {
            return BucketPostDesign;
        }

        if (Terminal.Contains(status.Value))
        {
            return BucketTerminal;
        }

        return BucketOther;
    }
}
