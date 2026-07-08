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

    public Task SendPaymentUpdatedAsync(
        PaymentUpdatedRealtimeDto payload,
        CancellationToken cancellationToken = default)
    {
        return _hub.Clients
            .Group(PaymentRealtimeConstants.Payment(payload.PaymentId))
            .SendAsync(
                PaymentRealtimeConstants.PaymentUpdatedEvent,
                payload,
                cancellationToken);
    }
}
