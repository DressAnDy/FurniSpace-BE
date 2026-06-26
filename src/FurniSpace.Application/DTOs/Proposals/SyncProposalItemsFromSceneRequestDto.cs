namespace FurniSpace.Application.DTOs.Proposals;

public sealed class SyncProposalItemsFromSceneRequestDto
{
    public Guid SceneId { get; set; }
    public List<SyncProposalItemFromSceneDto> Items { get; set; } = [];
}
