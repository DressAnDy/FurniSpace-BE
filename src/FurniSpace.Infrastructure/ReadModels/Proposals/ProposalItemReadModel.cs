namespace FurniSpace.Infrastructure.ReadModels.Proposals;

public class ProposalItemReadModel
{
    public Guid ProposalItemId { get; set; }
    public Guid ProposalId { get; set; }
    public Guid? SceneId { get; set; }
    public string? SceneObjectId { get; set; }
    public Guid? ProjectAreaId { get; set; }
    public string? ProjectAreaName { get; set; }
    public int? FloorNumber { get; set; }
    public Guid? ProductVersionId { get; set; }
    public string ProductNameSnapshot { get; set; } = string.Empty;
    public string? VersionNameSnapshot { get; set; }
    public string? MaterialSnapshot { get; set; }
    public string? ColorSnapshot { get; set; }
    public decimal? WidthSnapshot { get; set; }
    public decimal? HeightSnapshot { get; set; }
    public decimal? DepthSnapshot { get; set; }
    public string? DimensionUnit { get; set; }
    public int? Quantity { get; set; }
    public decimal? UnitPriceSnapshot { get; set; }
    public decimal? SubtotalAmount { get; set; }
    public string? CustomizationNote { get; set; }
}
