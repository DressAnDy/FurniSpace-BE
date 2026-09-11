using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.DTOs.ProjectChatMessages;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Application.Interfaces.ProjectChatMessages;

namespace FurniSpace.Testing.Fakes;

public sealed class NoOpRealtimeNotificationService : IRealtimeNotificationService
{
    public Task SendToUserAsync(
        Guid userId,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SendToRoleAsync(
        string role,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SendToUsersAsync(
        IEnumerable<Guid> userIds,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public sealed class NoOpProjectChatRealtimeService : IProjectChatRealtimeService
{
    public Task SendMessageSentAsync(
        Guid projectId,
        Guid chatId,
        ProjectChatMessageDto message,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public sealed class NoOpPaymentRealtimeService : IPaymentRealtimeService
{
    public Task SendPaymentUpdatedAsync(
        PaymentUpdatedRealtimeDto payload,
        IReadOnlyCollection<Guid>? stakeholderUserIds = null,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
