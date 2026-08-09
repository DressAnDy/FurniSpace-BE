using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Accounts;

public sealed class DesignerAssignedProjectReadModel
{
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public ProjectStatus? Status { get; set; }
    public DateTime? DesignerAssignedAt { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public string? SalesName { get; set; }
}
