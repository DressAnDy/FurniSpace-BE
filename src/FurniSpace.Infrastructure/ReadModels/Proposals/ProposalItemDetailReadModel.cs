using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Proposals;

public sealed class ProposalItemDetailReadModel : ProposalItemReadModel
{
    public Guid ProjectId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
    public ProposalStatus? ProposalStatus { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
