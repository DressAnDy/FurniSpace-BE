using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Orders;

public sealed class OrderListItemDto
{
    public Guid OrderId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid QuotationId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public decimal OriginalTotalAmount { get; set; }
    public decimal? DepositAmount { get; set; }
    public decimal? PaidAmount { get; set; }
    public decimal? RemainingAmount { get; set; }
    public OrderStatus? Status { get; set; }
    public DateTime? CreatedAt { get; set; }
}
