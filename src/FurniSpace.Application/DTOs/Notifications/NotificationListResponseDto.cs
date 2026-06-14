namespace FurniSpace.Application.DTOs.Notifications;

public sealed class NotificationListResponseDto
{
    public IReadOnlyList<NotificationDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int Limit { get; set; }
    public int Total { get; set; }
}
