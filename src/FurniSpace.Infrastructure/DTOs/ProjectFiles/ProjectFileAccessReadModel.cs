using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.DTOs.ProjectFiles;

public sealed class ProjectFileAccessReadModel
{
    public Guid ProjectId { get; init; }
    public Guid CustomerId { get; init; }
    public Guid? AssignedSalesId { get; init; }
    public Guid? AssignedDesignerId { get; init; }
    public ProjectStatus? Status { get; init; }
}
