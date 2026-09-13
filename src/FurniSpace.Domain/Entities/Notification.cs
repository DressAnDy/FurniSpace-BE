using System;

namespace FurniSpace.Domain.Entities;

public class Notification
{
    public Guid NotificationId { get; set; }
    public Guid ReceiverId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Title { get; set; } = null!;
    public string? Message { get; set; }
    public string? NotificationType { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
