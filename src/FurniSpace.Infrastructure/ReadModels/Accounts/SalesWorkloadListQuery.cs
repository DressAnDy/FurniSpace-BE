namespace FurniSpace.Infrastructure.ReadModels.Accounts;

public sealed class SalesWorkloadListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public int MaxActiveProjects { get; init; }
    public string? Search { get; init; }
    public string? CapacityState { get; init; }
    public string? FuturePressureState { get; init; }
    public string SortBy { get; init; } = "FuturePressureScoreDesc";
}
