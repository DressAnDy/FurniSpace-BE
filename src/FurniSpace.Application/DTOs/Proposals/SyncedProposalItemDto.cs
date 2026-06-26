namespace FurniSpace.Application.DTOs.Proposals;

public sealed class SyncedProposalItemDto
{
    public Guid ProposalItemId { get; set; }
    public string? SceneObjectId { get; set; }
    public Guid? ProductVersionId { get; set; }
    public string? ProductNameSnapshot { get; set; }
    public string? VersionNameSnapshot { get; set; }
    public int? Quantity { get; set; }
    public decimal? UnitPriceSnapshot { get; set; }
    public decimal? SubtotalAmount { get; set; }
    public string? CustomizationNote { get; set; }
}
