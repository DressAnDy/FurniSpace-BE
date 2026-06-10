using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.DTOs.Projects;

public sealed class ProjectListItemReadModel
{
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string? BusinessType { get; set; }
    public ProjectStatus? Status { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
    public DateTime? SubmittedAt { get; set; }
}
