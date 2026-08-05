using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Quotations;

public sealed class QuotationItemDto
{
    public const string ResourceName = "quotationItem";

    public Guid QuotationItemId { get; set; }
    public Guid QuotationId { get; set; }
    public QuotationItemType? ItemType { get; set; }
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
    public decimal? CustomizationUnitAdditionalCost { get; set; }
    public decimal? GrossAmount { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? TaxableAmount { get; set; }
    public decimal? TaxRate { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal? TotalAmount { get; set; }
    public bool? IsCustomized { get; set; }
    public string? CustomizationNote { get; set; }
    public string? Note { get; set; }
}
