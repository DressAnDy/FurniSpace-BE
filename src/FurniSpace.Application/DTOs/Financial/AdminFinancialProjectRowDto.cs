using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Financial;

public sealed class AdminFinancialProjectRowDto
{
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public ProjectStatus? ProjectStatus { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public string? AssignedSalesName { get; set; }
    public decimal? ProjectStartFeeAmount { get; set; }
    public PaymentStatus? ProjectStartFeeStatus { get; set; }
    public DateTime? ProjectStartFeePaidAt { get; set; }
    public Guid? OrderId { get; set; }
    public string? OrderCode { get; set; }
    public OrderStatus? OrderStatus { get; set; }
    public decimal? OrderOriginalTotal { get; set; }
    public decimal? OrderAdjustmentAmount { get; set; }
    public decimal? OrderAdditionalDiscount { get; set; }
    public decimal? OrderFinalTotal { get; set; }
    public decimal? OrderPaidAmount { get; set; }
    public decimal? OrderRemainingAmount { get; set; }
    public Guid? ActivePaymentId { get; set; }
    public PaymentType? ActivePaymentType { get; set; }
    public decimal? ActivePaymentAmount { get; set; }
    public PaymentStatus? ActivePaymentStatus { get; set; }
    public decimal TotalProjectCashCollected { get; set; }
    public DateTime? LastPaidAt { get; set; }
}
