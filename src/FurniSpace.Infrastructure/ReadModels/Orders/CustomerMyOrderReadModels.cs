using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Orders;

public sealed class CustomerMyOrdersQueryReadModel
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public OrderStatus? Status { get; set; }
    public string? Search { get; set; }
}

public sealed class CustomerMyOrderListItemReadModel
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public OrderStatus? Status { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal? DepositAmount { get; set; }
    public decimal? PaidAmount { get; set; }
    public decimal? RemainingAmount { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
