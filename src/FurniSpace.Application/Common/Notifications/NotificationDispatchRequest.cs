namespace FurniSpace.Application.Common.Notifications;

public sealed record NotificationDispatchRequest(
    Guid? ProjectId = null,
    string? ReferenceType = null,
    Guid? ReferenceId = null,
    IReadOnlyDictionary<string, object?>? Metadata = null);
