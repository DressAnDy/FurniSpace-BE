#nullable enable

namespace FurniSpace.Application.DTOs.Orders;

public sealed class UpdateDeliveredQuantityRequestDto
{
    public int? DeliveredQuantityIncrement { get; set; }
    public string? DeliveryNote { get; set; }
}

public sealed class OrderItemDeliveredQuantityDto
{
    public Guid OrderItemId { get; set; }
    public int Quantity { get; set; }
    public int DeliveredQuantity { get; set; }
    public DateTime? LastDeliveredAt { get; set; }
    public Guid? LastDeliveredBy { get; set; }
}
