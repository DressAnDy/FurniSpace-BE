using FurniSpace.Domain.Entities;

namespace FurniSpace.Application.Interfaces.Payments;

public interface IPaymentBusinessEffectService
{
    Task ApplyAsync(Payment payment, CancellationToken cancellationToken = default);
}
