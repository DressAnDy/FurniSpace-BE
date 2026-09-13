namespace FurniSpace.Infrastructure.ReadModels.Accounts;

public sealed class SalesWorkloadSummaryReadModel
{
    public int TotalActiveSales { get; set; }
    public int AvailableNowCount { get; set; }
    public int FullNowCount { get; set; }
    public int OverNowCount { get; set; }
    public int HighFuturePressureCount { get; set; }
    public int TotalSalesActiveProjects { get; set; }
    public int UnassignedIntakeCount { get; set; }
}
