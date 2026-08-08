namespace FurniSpace.Application.DTOs.Accounts;

public sealed class SalesWorkloadQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }

    /// <summary>AVAILABLE_NOW | FULL_NOW | OVER_NOW</summary>
    public string? CapacityState { get; set; }

    /// <summary>LOW | MEDIUM | HIGH</summary>
    public string? FuturePressureState { get; set; }

    /// <summary>FuturePressureScoreDesc (default) | SalesActiveCountDesc | AvailableSlotAsc</summary>
    public string? SortBy { get; set; }
}
