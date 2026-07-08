using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Orders;

public sealed class OrderListItemReadModel
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
    public Guid CustomerId { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
}

public sealed class OrderDetailReadModel
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
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
    public IReadOnlyList<OrderItemDetailReadModel> Items { get; set; } = [];
}

public sealed class OrderItemDetailReadModel
{
    public Guid OrderItemId { get; set; }
    public QuotationItemType? ItemType { get; set; }
    public string? ProductNameSnapshot { get; set; }
    public string? ItemName { get; set; }
    public int? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? CustomizationAdditionalCost { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? SubtotalAmount { get; set; }
    public bool? IsCustomized { get; set; }
}
