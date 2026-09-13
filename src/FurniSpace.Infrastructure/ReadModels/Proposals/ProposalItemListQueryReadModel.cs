namespace FurniSpace.Infrastructure.ReadModels.Proposals;

public sealed class ProposalItemListQueryReadModel
{
    public Guid ProposalId { get; set; }
    public Guid? SceneId { get; set; }
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 20;
}
