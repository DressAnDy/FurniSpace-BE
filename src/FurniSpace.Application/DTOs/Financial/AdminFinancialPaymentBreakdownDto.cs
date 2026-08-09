namespace FurniSpace.Application.DTOs.Financial;

public sealed class AdminFinancialPaymentBreakdownDto
{
    public string Currency { get; set; } = string.Empty;
    public List<AdminFinancialPaymentBreakdownItemDto> Items { get; set; } = [];
}
