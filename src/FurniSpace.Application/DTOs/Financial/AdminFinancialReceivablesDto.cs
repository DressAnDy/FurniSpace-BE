namespace FurniSpace.Application.DTOs.Financial;

public sealed class AdminFinancialReceivablesDto
{
    public decimal OutstandingPaymentAmount { get; set; }
    public int OutstandingPaymentCount { get; set; }
    public decimal ContractedReceivableAmount { get; set; }
    public int OrdersWithReceivableCount { get; set; }
    public int WithoutPaymentCount { get; set; }
    public int ActiveCollectionCount { get; set; }
    public int ExpiredPaymentCount { get; set; }
    public int FailedPaymentCount { get; set; }
    public List<AdminFinancialReceivableItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}
