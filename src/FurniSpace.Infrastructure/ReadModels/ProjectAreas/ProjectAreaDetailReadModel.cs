using FurniSpace.Domain.Enums;
using FurniSpace.Shared.DTOs.ProjectAreas;

namespace FurniSpace.Infrastructure.ReadModels.ProjectAreas;

public sealed class ProjectAreaDetailReadModel : ProjectAreaBaseDto<ProjectAreaType?, ProjectAreaStatus?>
{
    public Guid CustomerId { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
}
