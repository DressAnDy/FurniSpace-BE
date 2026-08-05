namespace FurniSpace.Application.DTOs.Quotations;

public sealed class UpdateManualQuotationItemRequestDto
{
    public string? ItemName { get; set; }
    public string? Description { get; set; }
    public int? DisplayOrder { get; set; }
    public int? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? CustomizationUnitAdditionalCost { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? TaxRate { get; set; }
    public string? Note { get; set; }
}
