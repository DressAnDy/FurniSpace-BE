using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Accounts;

public sealed class SalesAssignedProjectReadModel
{
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public ProjectStatus? Status { get; set; }
    public DateTime? SalesAssignedAt { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? AssignedDesignerId { get; set; }
    public string? DesignerName { get; set; }
}
