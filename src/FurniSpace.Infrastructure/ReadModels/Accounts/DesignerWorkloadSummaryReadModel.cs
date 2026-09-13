using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Accounts;

public sealed class DesignerWorkloadSummaryReadModel
{
    public int TotalActiveDesigners { get; set; }
    public int AvailableCount { get; set; }
    public int FullCount { get; set; }
    public int OverCount { get; set; }
    public int TotalDesignActiveProjects { get; set; }
}
