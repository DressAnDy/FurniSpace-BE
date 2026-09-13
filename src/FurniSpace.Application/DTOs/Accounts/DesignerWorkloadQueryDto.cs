namespace FurniSpace.Application.DTOs.Accounts;

public sealed class DesignerWorkloadQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }

    /// <summary>
    /// Optional: AVAILABLE | FULL | OVER
    /// </summary>
    public string? CapacityState { get; set; }

    /// <summary>
    /// Optional: DesignActiveCountDesc (default) | AvailableSlotDesc
    /// </summary>
    public string? SortBy { get; set; }
}
