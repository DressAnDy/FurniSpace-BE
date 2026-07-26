#nullable enable

namespace FurniSpace.Application.DTOs.Orders;

public sealed class OrderItemDeliveryConfirmationDto
{
    public Guid OrderItemId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? CustomerConfirmedAt { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
}
