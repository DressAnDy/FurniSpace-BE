namespace FurniSpace.Infrastructure.ReadModels.Financial;

public sealed class AdminFinancialReceivablesSummaryReadModel
{
    public decimal OutstandingPaymentAmount { get; set; }
    public int OutstandingPaymentCount { get; set; }
    public decimal ContractedReceivableAmount { get; set; }
    public int OrdersWithReceivableCount { get; set; }
    public int WithoutPaymentCount { get; set; }
    public int ActiveCollectionCount { get; set; }
    public int ExpiredPaymentCount { get; set; }
    public int FailedPaymentCount { get; set; }
}
