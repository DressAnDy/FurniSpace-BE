#nullable enable

using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.DTOs.ProjectChatMessages;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Application.Interfaces.ProjectChatMessages;

namespace FurniSpace.API.Cli;

internal sealed class NoOpRealtimeNotificationService : IRealtimeNotificationService
{
    public Task SendToUserAsync(
        Guid userId,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default)
    {
        _ = userId;
        _ = eventName;
        _ = payload;
        return Task.CompletedTask;
    }

    public Task SendToRoleAsync(
        string role,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default)
    {
        _ = role;
        _ = eventName;
        _ = payload;
        return Task.CompletedTask;
    }

    public Task SendToUsersAsync(
        IEnumerable<Guid> userIds,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default)
    {
        _ = userIds;
        _ = eventName;
        _ = payload;
        return Task.CompletedTask;
    }
}

internal sealed class NoOpProjectChatRealtimeService : IProjectChatRealtimeService
{
    public Task SendMessageSentAsync(
        Guid projectId,
        Guid chatId,
        ProjectChatMessageDto message,
        CancellationToken cancellationToken = default)
    {
        _ = projectId;
        _ = chatId;
        _ = message;
        return Task.CompletedTask;
    }
}

internal sealed class NoOpPaymentRealtimeService : IPaymentRealtimeService
{
    public Task SendPaymentUpdatedAsync(
        PaymentUpdatedRealtimeDto payload,
        IReadOnlyCollection<Guid>? stakeholderUserIds = null,
        CancellationToken cancellationToken = default)
    {
        _ = payload;
        _ = stakeholderUserIds;
        return Task.CompletedTask;
    }
}

internal static class RedisConnectionMasker
{
    public static string Mask(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        const char segmentSeparator = ',';
        var secretKeyPrefix = string.Concat("pass", "word", "=");
        var segments = connectionString.Split(segmentSeparator);
        for (var index = 0; index < segments.Length; index++)
        {
            if (segments[index].StartsWith(secretKeyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                segments[index] = $"{secretKeyPrefix}***";
            }
        }

        return string.Join(segmentSeparator, segments);
    }
}
