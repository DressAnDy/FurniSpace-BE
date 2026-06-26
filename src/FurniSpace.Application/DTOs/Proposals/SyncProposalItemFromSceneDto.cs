namespace FurniSpace.Application.DTOs.Proposals;

public sealed class SyncProposalItemFromSceneDto
{
    public string? SceneObjectId { get; set; }
    public Guid ProductVersionId { get; set; }
    public int Quantity { get; set; }
    public string? CustomizationNote { get; set; }
}
