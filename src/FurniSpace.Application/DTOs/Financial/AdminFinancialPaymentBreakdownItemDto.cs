using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Financial;

public sealed class AdminFinancialPaymentBreakdownItemDto
{
    public PaymentType PaymentType { get; set; }
    public decimal CollectedAmount { get; set; }
    public int PaidCount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public int OutstandingCount { get; set; }
    public int ExpiredCount { get; set; }
}
