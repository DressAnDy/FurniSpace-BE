namespace FurniSpace.Infrastructure.ReadModels.Financial;

public sealed class AdminFinancialSummaryReadModel
{
    public decimal CollectedAmount { get; set; }
    public decimal OutstandingPaymentAmount { get; set; }
    public decimal ContractedReceivableAmount { get; set; }
    public decimal OrderCommercialValue { get; set; }
    public int FailedTransactionCount { get; set; }
    public int ActivePaymentCount { get; set; }
}
