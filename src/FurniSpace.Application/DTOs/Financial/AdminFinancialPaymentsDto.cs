namespace FurniSpace.Application.DTOs.Financial;

public sealed class AdminFinancialPaymentsDto
{
    public List<AdminFinancialPaymentRowDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}
