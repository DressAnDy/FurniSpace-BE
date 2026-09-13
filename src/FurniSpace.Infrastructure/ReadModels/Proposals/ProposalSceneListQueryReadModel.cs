using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Proposals;

public sealed class ProposalSceneListQueryReadModel
{
    public Guid ProposalId { get; set; }
    public ProposalSceneType? SceneType { get; set; }
    public bool? IsActive { get; set; }
    public bool ActiveOnly { get; set; }
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 20;
}
