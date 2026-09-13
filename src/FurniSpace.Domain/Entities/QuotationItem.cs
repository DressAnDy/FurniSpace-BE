using System;

namespace FurniSpace.Domain.Entities;

public class QuotationItem
{
    public Guid QuotationItemId { get; set; }
    public Guid QuotationId { get; set; }
    public Guid? ProposalItemId { get; set; }
    public Guid? ProductVersionId { get; set; }
    public string? ProductNameSnapshot { get; set; }
    public string? ProductVersionNameSnapshot { get; set; }
    public string? ProductVersionCodeSnapshot { get; set; }
    public string? ItemName { get; set; }
    public string? Description { get; set; }
    public int? DisplayOrder { get; set; }
    public int? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? GrossAmount { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? TotalAmount { get; set; }
    public bool? IsCustomized { get; set; }
    public string? CustomizationNote { get; set; }
    public string? Note { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
