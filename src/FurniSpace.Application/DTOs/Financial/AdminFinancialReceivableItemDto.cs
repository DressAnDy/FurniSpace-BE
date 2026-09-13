#nullable enable

using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Financial;

public sealed class AdminFinancialReceivableItemDto
{
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public OrderStatus? OrderStatus { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public decimal FinalTotalAmount { get; set; }
    public decimal? PaidAmount { get; set; }
    public decimal? RemainingAmount { get; set; }
    public decimal PaymentProgressPercentage { get; set; }
    public string CollectionState { get; set; } = string.Empty;
    public int ReceivableAgeDays { get; set; }
    public DateTimeOffset? LastPaidAt { get; set; }
    public Guid? ActivePaymentId { get; set; }
    public PaymentType? ActivePaymentType { get; set; }
    public decimal? ActivePaymentAmount { get; set; }
    public PaymentStatus? ActivePaymentStatus { get; set; }
    public DateTimeOffset? ActivePaymentExpiredAt { get; set; }
    public string? LastPaymentFailureReason { get; set; }
    public bool IsPaymentCreated { get; set; }
}
