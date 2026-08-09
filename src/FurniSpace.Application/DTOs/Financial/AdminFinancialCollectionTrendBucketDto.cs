namespace FurniSpace.Application.DTOs.Financial;

public sealed class AdminFinancialCollectionTrendBucketDto
{
    public string Period { get; set; } = string.Empty;
    public decimal ProjectStartFee { get; set; }
    public decimal Deposit { get; set; }
    public decimal RemainingPayment { get; set; }
    public decimal Total { get; set; }
}
