namespace FurniSpace.Infrastructure.ReadModels.Financial;

public sealed class AdminFinancialReceivablesSummaryReadModel
{
    public decimal OutstandingPaymentAmount { get; set; }
    public int OutstandingPaymentCount { get; set; }
    public decimal ContractedReceivableAmount { get; set; }
    public int OrdersWithReceivableCount { get; set; }
}
