namespace FurniSpace.Application.DTOs.Quotations;

public sealed class UpdateQuotationItemFinancialsRequestDto
{
    public int? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? CustomizationUnitAdditionalCost { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? TaxRate { get; set; }
}
