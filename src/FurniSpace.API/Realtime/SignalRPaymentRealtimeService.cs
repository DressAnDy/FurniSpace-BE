#nullable enable

using FurniSpace.API.Hubs;
using FurniSpace.Application.Common.Realtime;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.Interfaces.Payments;
using Microsoft.AspNetCore.SignalR;

namespace FurniSpace.API.Realtime;

public sealed class SignalRPaymentRealtimeService : IPaymentRealtimeService
{
    private readonly IHubContext<PaymentHub> _hub;

    public SignalRPaymentRealtimeService(IHubContext<PaymentHub> hub)
    {
        _hub = hub;
    }

    public async Task SendPaymentUpdatedAsync(
        PaymentUpdatedRealtimeDto payload,
        IReadOnlyCollection<Guid>? stakeholderUserIds = null,
        CancellationToken cancellationToken = default)
    {
        await _hub.Clients
            .Group(PaymentRealtimeConstants.Payment(payload.PaymentId))
            .SendAsync(
                PaymentRealtimeConstants.PaymentUpdatedEvent,
                payload,
                cancellationToken);

        if (stakeholderUserIds is null || stakeholderUserIds.Count == 0)
        {
            return;
        }

        foreach (var userId in stakeholderUserIds.Where(id => id != Guid.Empty).Distinct())
        {
            await _hub.Clients
                .Group(RealtimeGroupNames.User(userId))
                .SendAsync(
                    PaymentRealtimeConstants.PaymentUpdatedEvent,
                    payload,
                    cancellationToken);
        }
    }
}
