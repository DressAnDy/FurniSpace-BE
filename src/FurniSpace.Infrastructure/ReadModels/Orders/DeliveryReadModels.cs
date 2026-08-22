using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Orders;

public sealed class DeliveryListItemReadModel
{
    public Guid DeliveryId { get; init; }
    public Guid OrderId { get; init; }
    public DeliveryStatus? Status { get; init; }
    public Guid? CreatedBy { get; init; }
    public Guid? CompletedBy { get; init; }
    public string? Note { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int ItemCount { get; init; }
}

public sealed class DeliveryDetailReadModel
{
    public Guid DeliveryId { get; init; }
    public Guid OrderId { get; init; }
    public DeliveryStatus? Status { get; init; }
    public Guid? CreatedBy { get; init; }
    public Guid? CompletedBy { get; init; }
    public string? Note { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int ItemCount { get; init; }
    public IReadOnlyList<DeliveryItemReadModel> Items { get; init; } = [];
}

public sealed class DeliveryItemReadModel
{
    public Guid DeliveryItemId { get; init; }
    public Guid DeliveryId { get; init; }
    public Guid OrderItemId { get; init; }
    public int Quantity { get; init; }
    public string? Note { get; init; }
    public string? ProductNameSnapshot { get; init; }
    public string? ItemName { get; init; }
}
