using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Orders;

public sealed class CustomerMyOrdersQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public OrderStatus? Status { get; set; }
    public string? Search { get; set; }
}

public sealed class CustomerMyOrderItemDto
{
    public Guid OrderId { get; init; }
    public string OrderCode { get; init; } = string.Empty;
    public Guid ProjectId { get; init; }
    public OrderStatus? Status { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal? DepositAmount { get; init; }
    public decimal? PaidAmount { get; init; }
    public decimal? RemainingAmount { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class CustomerMyOrdersResponseDto
{
    public IReadOnlyList<CustomerMyOrderItemDto> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}
