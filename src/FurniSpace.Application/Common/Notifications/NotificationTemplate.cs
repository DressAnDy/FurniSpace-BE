namespace FurniSpace.Application.Common.Notifications;

public sealed record NotificationTemplate(
    string TitleTemplate,
    string MessageTemplate,
    NotificationDeliveryLevel DeliveryLevel,
    string SignalREventName = "notification.created");
