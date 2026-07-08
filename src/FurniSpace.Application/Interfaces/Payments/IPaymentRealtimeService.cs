using FurniSpace.Application.DTOs.Payments;

namespace FurniSpace.Application.Interfaces.Payments;

public interface IPaymentRealtimeService
{
    Task SendPaymentUpdatedAsync(
        PaymentUpdatedRealtimeDto payload,
        CancellationToken cancellationToken = default);
}
