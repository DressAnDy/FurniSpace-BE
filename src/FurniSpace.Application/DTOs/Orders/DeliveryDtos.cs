using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Orders;

public sealed class DeliveryItemDto
{
    public Guid DeliveryItemId { get; init; }
    public Guid DeliveryId { get; init; }
    public Guid OrderItemId { get; init; }
    public int Quantity { get; init; }
    public string? Note { get; init; }
    public string? ProductNameSnapshot { get; init; }
    public string? ItemName { get; init; }
}

public sealed class DeliveryListItemDto
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

public sealed class DeliveryDetailDto
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
    public IReadOnlyList<DeliveryItemDto> Items { get; init; } = [];
}

public sealed class DeliveryListResponseDto
{
    public IReadOnlyList<DeliveryListItemDto> Items { get; init; } = [];
}

public sealed class DeliveryBatchCompletionDto
{
    public Guid DeliveryId { get; init; }
    public Guid OrderId { get; init; }
    public DeliveryStatus? Status { get; init; }
    public int UpdatedItemCount { get; init; }
    public DateTime? CompletedAt { get; init; }
}
