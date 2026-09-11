namespace FurniSpace.Application.DTOs.Orders;

public sealed class OrderDeliveryConfirmationDto
{
    public Guid OrderId { get; set; }
    public Guid ProjectId { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public string ProjectStatus { get; set; } = string.Empty;
    public DateTime? CustomerConfirmedDeliveryAt { get; set; }
}
