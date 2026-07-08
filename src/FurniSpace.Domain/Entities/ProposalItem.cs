using System;

namespace FurniSpace.Domain.Entities;

public class ProposalItem
{
    public Guid ProposalItemId { get; set; }
    public Guid ProposalId { get; set; }
    public Guid? SceneId { get; set; }
    public string? SceneObjectId { get; set; }
    public Guid? ProjectAreaId { get; set; }
    public Guid? ProductVersionId { get; set; }
    public Guid? ApprovedProductVersionId { get; set; }
    public string ItemName { get; set; } = null!;
    public string? ItemType { get; set; }
    public int? Quantity { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public decimal? Depth { get; set; }
    public string? Material { get; set; }
    public string? Color { get; set; }
    public bool? IsCustomized { get; set; }
    public decimal? UnitPriceSnapshot { get; set; }
    public decimal? TotalPriceSnapshot { get; set; }
    public string? Note { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}


