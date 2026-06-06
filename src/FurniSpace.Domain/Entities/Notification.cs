using System;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Domain.Entities;

public class Notification
{
    public Guid NotificationId { get; set; }
    public Guid ReceiverId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Title { get; set; } = null!;
    public string? Message { get; set; }
    public string? NotificationType { get; set; }
    public NotificationStatus? Status { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

