namespace FurniSpace.Infrastructure.DTOs.Proposals;

public sealed class ProposalItemReadModel
{
    public Guid ProposalItemId { get; set; }
    public Guid? SceneId { get; set; }
    public string? SceneObjectId { get; set; }
    public Guid? ProductVersionId { get; set; }
    public string ProductNameSnapshot { get; set; } = string.Empty;
    public string? VersionNameSnapshot { get; set; }
    public int? Quantity { get; set; }
    public decimal? UnitPriceSnapshot { get; set; }
    public decimal? SubtotalAmount { get; set; }
}
