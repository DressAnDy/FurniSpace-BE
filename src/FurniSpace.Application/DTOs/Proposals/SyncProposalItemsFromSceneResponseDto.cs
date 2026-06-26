namespace FurniSpace.Application.DTOs.Proposals;

public sealed class SyncProposalItemsFromSceneResponseDto
{
    public Guid ProposalId { get; set; }
    public Guid SceneId { get; set; }
    public List<SyncedProposalItemDto> Items { get; set; } = [];
}
