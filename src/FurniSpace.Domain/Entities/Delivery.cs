using System;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Domain.Entities;

public class Delivery
{
    public Guid DeliveryId { get; set; }
    public Guid OrderId { get; set; }
    public Guid? ProjectScheduleId { get; set; }
    public DeliveryStatus? Status { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? CompletedBy { get; set; }
    public string? Note { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
