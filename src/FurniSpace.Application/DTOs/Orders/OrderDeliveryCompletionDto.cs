namespace FurniSpace.Application.DTOs.Orders;

public sealed class OrderDeliveryCompletionDto
{
    public Guid OrderId { get; set; }
    public Guid ProjectId { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public int DeliveredItemCount { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
