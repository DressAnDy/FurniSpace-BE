using FurniSpace.Domain.Common;

namespace FurniSpace.Infrastructure.ReadModels.Orders;

public sealed class OrderListItemReadModel : OrderListItemShape
{
    public Guid CustomerId { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
}

public sealed class OrderDetailReadModel : OrderDetailShape
{
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
    public IReadOnlyList<OrderItemDetailReadModel> Items { get; set; } = [];
}

public sealed class OrderItemDetailReadModel : OrderItemShape;
