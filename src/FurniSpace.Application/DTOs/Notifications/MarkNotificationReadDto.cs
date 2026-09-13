namespace FurniSpace.Application.DTOs.Notifications;

public sealed class MarkNotificationReadDto
{
    public Guid NotificationId { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
}
