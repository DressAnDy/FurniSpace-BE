using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Projects;

public sealed class ProjectListQueryReadModel
{
    public ProjectStatus? Status { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 20;
}
