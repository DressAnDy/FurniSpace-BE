namespace FurniSpace.Application.DTOs.Accounts;

public sealed class SalesWorkloadItemDto
{
    public Guid AccountId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Status { get; set; }

    public int IntakeCount { get; set; }
    public int CommercialCount { get; set; }
    public int DesignMonitorCount { get; set; }
    public int FulfillmentCount { get; set; }
    public int SalesActiveCount { get; set; }
    public int LifecycleAssignedCount { get; set; }

    public int MaxActiveProjects { get; set; }
    public int AvailableSlot { get; set; }

    /// <summary>AVAILABLE_NOW | FULL_NOW | OVER_NOW</summary>
    public string CapacityState { get; set; } = string.Empty;

    public decimal FuturePressureScore { get; set; }

    /// <summary>LOW | MEDIUM | HIGH</summary>
    public string FuturePressureState { get; set; } = string.Empty;

    public int ApproachingCommercialCount { get; set; }
    public int ProductionAttentionCount { get; set; }
    public int DeliveryAttentionCount { get; set; }

    public SalesFuturePressureBreakdownDto FuturePressureBreakdown { get; set; } = new();

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
