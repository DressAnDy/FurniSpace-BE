using FurniSpace.Application.Common.Notifications;

namespace FurniSpace.Testing.Fakes;

public sealed record CapturedNotification(
    NotificationType Type,
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyList<Guid> ReceiverIds,
    Guid? ProjectId,
    string? ReferenceType,
    Guid? ReferenceId,
    IReadOnlyDictionary<string, object?>? Metadata = null);
