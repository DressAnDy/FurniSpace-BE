using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Financial;

public sealed class AdminFinancialReceivableItemDto
{
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public OrderStatus? OrderStatus { get; set; }
    public decimal FinalTotalAmount { get; set; }
    public decimal? PaidAmount { get; set; }
    public decimal? RemainingAmount { get; set; }
    public Guid? ActivePaymentId { get; set; }
    public PaymentType? ActivePaymentType { get; set; }
    public decimal? ActivePaymentAmount { get; set; }
    public PaymentStatus? ActivePaymentStatus { get; set; }
    public bool IsPaymentCreated { get; set; }
}
