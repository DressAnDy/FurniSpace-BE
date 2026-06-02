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
    public int? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? CustomizationFee { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? SubtotalAmount { get; set; }
    public string? Note { get; set; }
}


