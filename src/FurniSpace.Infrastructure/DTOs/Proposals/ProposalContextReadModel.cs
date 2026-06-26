using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.DTOs.Proposals;

public sealed class ProposalContextReadModel
{
    public Guid ProposalId { get; set; }
    public Guid ProjectId { get; set; }
    public ProposalStatus? ProposalStatus { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
}
