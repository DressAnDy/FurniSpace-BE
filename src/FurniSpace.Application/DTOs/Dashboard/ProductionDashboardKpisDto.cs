namespace FurniSpace.Application.DTOs.Dashboard;

public sealed class ProductionDashboardKpisDto
{
    public int PendingReview { get; set; }

    public int InProduction { get; set; }

    public int ReadyToComplete { get; set; }

    public int OverdueTasks { get; set; }
}
