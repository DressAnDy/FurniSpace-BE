using System;

namespace FurniSpace.Domain.Entities;

public class MeasurementAppointment
{
    public Guid AppointmentId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProjectAreaId { get; set; }
    public Guid? ScheduledBySalesId { get; set; }
    public Guid? DesignerId { get; set; }
    public DateTime AppointmentTime { get; set; }
    public string? AppointmentAddress { get; set; }
    public string? Status { get; set; }
    public string? CustomerNote { get; set; }
    public string? InternalNote { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}


