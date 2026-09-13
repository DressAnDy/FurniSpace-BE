using System;

namespace FurniSpace.Domain.Entities;

public class DeliveryItem
{
    public Guid DeliveryItemId { get; set; }
    public Guid DeliveryId { get; set; }
    public Guid OrderItemId { get; set; }
    public int Quantity { get; set; }
    public string? Note { get; set; }
}
