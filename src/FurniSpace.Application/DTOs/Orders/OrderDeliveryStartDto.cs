#nullable enable

namespace FurniSpace.Application.DTOs.Orders;

public sealed class OrderDeliveryStartDto
{
    public Guid OrderId { get; set; }
    public Guid ProjectId { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public string ProjectStatus { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
}
