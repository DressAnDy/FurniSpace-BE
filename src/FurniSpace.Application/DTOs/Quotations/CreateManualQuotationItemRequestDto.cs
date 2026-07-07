namespace FurniSpace.Application.DTOs.Quotations;

public sealed class CreateManualQuotationItemRequestDto
{
    public string? ItemName { get; set; }
    public string? Description { get; set; }
    public int? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? DiscountAmount { get; set; }
    public string? Note { get; set; }
}
