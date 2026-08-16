namespace FurniSpace.Application.DTOs.Notifications;

public sealed class RealtimeNotificationPayloadDto
{
    public Guid? NotificationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public Guid? ProjectId { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime OccurredAt { get; set; }
    public IReadOnlyDictionary<string, object?>? Metadata { get; set; }
}
