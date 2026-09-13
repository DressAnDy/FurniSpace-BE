namespace FurniSpace.Application.DTOs.Dashboard;

public sealed class ProductionDashboardKpisDto
{
    public int PendingCustomizationReview { get; set; }

    public int PendingStart { get; set; }

    /// <summary>Deprecated alias of <see cref="PendingStart"/> for FE compatibility.</summary>
    public int PendingReview { get; set; }

    public int InProduction { get; set; }

    public int ReadyToComplete { get; set; }

    public int OverdueTasks { get; set; }

    public int ReadyForDelivery { get; set; }

    public int AwaitingDeliverySchedule { get; set; }

    public int CompletedInRange { get; set; }
}
