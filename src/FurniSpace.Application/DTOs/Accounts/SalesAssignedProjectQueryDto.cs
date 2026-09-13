namespace FurniSpace.Application.DTOs.Accounts;

public sealed class SalesAssignedProjectQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// CURRENT_ACTIVE | INTAKE | COMMERCIAL | DESIGN_MONITOR | FULFILLMENT | TERMINAL | OTHER | HIGH_PRESSURE_SOURCE
    /// </summary>
    public string? Bucket { get; set; }
}
