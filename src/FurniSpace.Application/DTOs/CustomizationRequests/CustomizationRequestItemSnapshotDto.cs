namespace FurniSpace.Application.DTOs.CustomizationRequests;

public sealed class CustomizationRequestItemSnapshotDto
{
    public Guid ProposalItemId { get; set; }
    public Guid ProposalId { get; set; }
    public Guid? ProductVersionId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? ItemType { get; set; }
    public int? Quantity { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public decimal? Depth { get; set; }
    public string? Material { get; set; }
    public string? Color { get; set; }
    public decimal? UnitPriceSnapshot { get; set; }
    public decimal? TotalPriceSnapshot { get; set; }
    public string? Note { get; set; }
}
