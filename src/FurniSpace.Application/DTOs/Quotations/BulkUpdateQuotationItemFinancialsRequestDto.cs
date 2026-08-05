namespace FurniSpace.Application.DTOs.Quotations;

public sealed class BulkUpdateQuotationItemFinancialsRequestDto
{
    public List<BulkUpdateQuotationItemFinancialsItemDto> Items { get; set; } = [];
}

public sealed class BulkUpdateQuotationItemFinancialsItemDto
{
    public Guid QuotationItemId { get; set; }
    public int? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? CustomizationUnitAdditionalCost { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? TaxRate { get; set; }
}
