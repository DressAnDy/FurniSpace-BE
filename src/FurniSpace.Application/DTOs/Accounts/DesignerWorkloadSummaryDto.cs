namespace FurniSpace.Application.DTOs.Accounts;

public sealed class DesignerWorkloadSummaryDto
{
    public int TotalActiveDesigners { get; set; }
    public int AvailableCount { get; set; }
    public int FullCount { get; set; }
    public int OverCount { get; set; }
    public int TotalDesignActiveProjects { get; set; }
    public int MaxActiveProjects { get; set; }
}
