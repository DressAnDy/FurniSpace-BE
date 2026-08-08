namespace FurniSpace.Application.DTOs.Accounts;

public sealed class DesignerAssignedProjectQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Optional: DESIGN_ACTIVE | POST_DESIGN | TERMINAL | OTHER
    /// </summary>
    public string? Bucket { get; set; }
}
