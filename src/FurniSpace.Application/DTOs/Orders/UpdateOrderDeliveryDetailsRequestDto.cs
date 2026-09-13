namespace FurniSpace.Application.DTOs.Orders;

public sealed class UpdateOrderDeliveryDetailsRequestDto
{
    public string? DeliveryAddress { get; set; }
    public string? ReceiverName { get; set; }
    public string? ReceiverPhone { get; set; }
    public string? DeliveryNote { get; set; }
}
