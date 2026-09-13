namespace FurniSpace.Application.DTOs.Accounts;

public sealed class SalesWorkloadSummaryDto
{
    public int TotalActiveSales { get; set; }
    public int AvailableNowCount { get; set; }
    public int FullNowCount { get; set; }
    public int OverNowCount { get; set; }
    public int HighFuturePressureCount { get; set; }
    public int TotalSalesActiveProjects { get; set; }
    public int UnassignedIntakeCount { get; set; }
    public int MaxActiveProjects { get; set; }
}
