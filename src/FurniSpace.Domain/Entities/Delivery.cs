using System;

namespace FurniSpace.Domain.Entities;

public class Delivery
{
    public Guid DeliveryId { get; set; }
    public string? DeliveryCode { get; set; }
    public Guid ProjectId { get; set; }
    public Guid OrderId { get; set; }
    public Guid? AssignedDeliveryStaffId { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? ReceiverName { get; set; }
    public string? ReceiverPhone { get; set; }
    public DateTime? ScheduledDeliveryDate { get; set; }
    public string? Status { get; set; }
    public string? DeliveryNote { get; set; }
    public string? FailedReason { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}


