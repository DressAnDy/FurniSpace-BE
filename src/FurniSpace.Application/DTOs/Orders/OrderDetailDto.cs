using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Orders;

public sealed class OrderDetailDto
{
    public Guid OrderId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProposalId { get; set; }
    public Guid QuotationId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid? SalesId { get; set; }
    public decimal OriginalTotalAmount { get; set; }
    public decimal? ItemAdjustmentAmount { get; set; }
    public decimal? AdditionalDiscountAmount { get; set; }
    public decimal FinalTotalAmount { get; set; }
    public decimal? DepositAmount { get; set; }
    public decimal? PaidAmount { get; set; }
    public decimal? RemainingAmount { get; set; }
    public OrderStatus? Status { get; set; }
    public IReadOnlyList<OrderItemDto> Items { get; init; } = [];
}
