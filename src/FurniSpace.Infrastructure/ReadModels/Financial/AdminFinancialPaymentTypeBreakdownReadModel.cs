using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Financial;

public sealed class AdminFinancialPaymentTypeBreakdownReadModel
{
    public PaymentType PaymentType { get; set; }
    public decimal CollectedAmount { get; set; }
    public int PaidCount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public int OutstandingCount { get; set; }
    public int ExpiredCount { get; set; }
}
