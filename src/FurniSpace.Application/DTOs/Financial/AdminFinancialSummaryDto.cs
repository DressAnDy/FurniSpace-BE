namespace FurniSpace.Application.DTOs.Financial;

public sealed class AdminFinancialSummaryDto
{
    public AdminFinancialPeriodDto Period { get; set; } = new();
    public string Currency { get; set; } = "VND";
    public decimal CollectedAmount { get; set; }
    public decimal OutstandingPaymentAmount { get; set; }
    public decimal ContractedReceivableAmount { get; set; }
    public decimal OrderCommercialValue { get; set; }
    public int FailedTransactionCount { get; set; }
    public int ActivePaymentCount { get; set; }
}
