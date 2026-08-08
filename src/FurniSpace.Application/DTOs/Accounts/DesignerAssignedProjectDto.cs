namespace FurniSpace.Application.DTOs.Accounts;

public sealed class DesignerAssignedProjectDto
{
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string? Status { get; set; }
    public DateTime? DesignerAssignedAt { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public string? SalesName { get; set; }

    /// <summary>
    /// DESIGN_ACTIVE | POST_DESIGN | TERMINAL | OTHER
    /// </summary>
    public string Bucket { get; set; } = string.Empty;
}
