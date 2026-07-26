using FurniSpace.Domain.Enums;
using Mapster;

namespace FurniSpace.Application.DTOs.Payments;

public sealed class PaymentProjectSummaryDto
{
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string? ProjectName { get; set; }
}

public sealed class PaymentOrderSummaryDto
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public OrderStatus? Status { get; set; }
    public decimal FinalTotalAmount { get; set; }
    public decimal? DepositAmount { get; set; }
    public decimal? PaidAmount { get; set; }
    public decimal? RemainingAmount { get; set; }
}

public sealed class PaymentLatestTransactionDto
{
    public Guid PaymentTransactionId { get; set; }
    public string TransactionCode { get; set; } = string.Empty;
    public PaymentProvider? PaymentProvider { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public PaymentTransactionStatus? Status { get; set; }
    public string? PaymentUrl { get; set; }
    public string? QrContent { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public sealed class PaymentDetailDto : PaymentDto
{
    public bool IsPayable { get; set; }
    public bool? Reused { get; set; }
    public PaymentProjectSummaryDto? Project { get; set; }
    public PaymentOrderSummaryDto? Order { get; set; }
    public PaymentLatestTransactionDto? LatestTransaction { get; set; }

    public static PaymentDetailDto From(PaymentDto source)
    {
        return source.Adapt<PaymentDetailDto>();
    }
}
