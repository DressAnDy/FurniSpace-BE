namespace FurniSpace.Infrastructure.DTOs.Proposals;

public sealed class ProposalDetailReadModel : ProposalReadModel
{
    public Guid CustomerId { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
    public IReadOnlyList<ProposalSceneReadModel> Scenes { get; set; } = [];
    public IReadOnlyList<ProposalItemReadModel> Items { get; set; } = [];
}
