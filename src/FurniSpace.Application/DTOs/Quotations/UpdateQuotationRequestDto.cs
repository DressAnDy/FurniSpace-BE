namespace FurniSpace.Application.DTOs.Quotations;

public sealed class UpdateQuotationRequestDto
{
    public DateOnly? ValidUntil { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? TaxAmount { get; set; }
    public string? CustomerNote { get; set; }
    public string? SalesNote { get; set; }
    public string? RevisionReason { get; set; }
}
