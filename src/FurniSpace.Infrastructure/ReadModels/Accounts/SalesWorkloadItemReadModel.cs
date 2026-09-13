using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Accounts;

public sealed class SalesWorkloadItemReadModel
{
    public Guid AccountId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public AccountStatus? Status { get; set; }

    public int IntakeCount { get; set; }
    public int CommercialCount { get; set; }
    public int DesignMonitorCount { get; set; }
    public int FulfillmentCount { get; set; }
    public int SalesActiveCount { get; set; }
    public int LifecycleAssignedCount { get; set; }

    public int MaxActiveProjects { get; set; }
    public int AvailableSlot { get; set; }
    public string CapacityState { get; set; } = string.Empty;

    public int MeasurementRequiredCount { get; set; }
    public int SpaceVerifiedCount { get; set; }
    public int ProposalConsultingCount { get; set; }
    public int InProductionCount { get; set; }
    public int ProductionBlockedCount { get; set; }
    public int ReadyForDeliveryCount { get; set; }
    public int DeliveringCount { get; set; }
    public int DeliveredCount { get; set; }

    public decimal FuturePressureScore { get; set; }
    public string FuturePressureState { get; set; } = string.Empty;

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
