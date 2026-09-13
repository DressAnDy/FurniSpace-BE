namespace FurniSpace.Application.DTOs.Proposals;

public sealed class SyncProposalItemsFromSceneResponseDto
{
    public Guid ProposalId { get; set; }
    public Guid SceneId { get; set; }
    public List<SyncedProposalItemDto> Items { get; set; } = [];
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int RemovedCount { get; set; }
}
