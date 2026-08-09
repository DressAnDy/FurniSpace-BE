namespace FurniSpace.Application.DTOs.Accounts;

public sealed class SalesAssignedProjectDto
{
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string? Status { get; set; }
    public DateTime? SalesAssignedAt { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? AssignedDesignerId { get; set; }
    public string? DesignerName { get; set; }

    /// <summary>INTAKE | COMMERCIAL | DESIGN_MONITOR | FULFILLMENT | TERMINAL | OTHER</summary>
    public string Bucket { get; set; } = string.Empty;

    public decimal PressureWeight { get; set; }
}
