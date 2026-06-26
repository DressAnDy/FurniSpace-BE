using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.DTOs.Proposals;

public sealed class ProposalProjectAccessReadModel
{
    public Guid ProjectId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
    public ProjectStatus? ProjectStatus { get; set; }
}
