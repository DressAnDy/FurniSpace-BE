namespace FurniSpace.Infrastructure.ReadModels.Proposals;

public sealed class ProposalSceneDetailReadModel
    : FurniSpace.Shared.DTOs.Proposals.ProposalSceneBaseDto<FurniSpace.Domain.Enums.ProposalSceneType?>
{
    public Guid ProjectId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
    public FurniSpace.Domain.Enums.ProposalStatus? ProposalStatus { get; set; }
}
