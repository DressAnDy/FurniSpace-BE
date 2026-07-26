using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.Payments;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;

namespace FurniSpace.Application.Common.Payments;

internal static class PaymentServiceActivePaymentSupport
{
    internal static async Task<Payment?> ResolveReusableActivePaymentAsync(
        IPaymentRepository payments,
        IUnitOfWork unitOfWork,
        Payment? existing,
        CancellationToken cancellationToken)
    {
        if (existing is null)
        {
            return null;
        }

        if (existing.Status == PaymentStatus.PAID)
        {
            return existing;
        }

        var now = DateTime.UtcNow;
        if (ActivePaymentResolver.IsActive(existing, now))
        {
            return existing;
        }

        if (existing.Status.HasValue &&
            ActivePaymentResolver.ActiveStatuses.Contains(existing.Status.Value) &&
            ActivePaymentResolver.IsExpired(existing, now))
        {
            ActivePaymentResolver.MarkExpired(existing, now);
            payments.UpdatePayment(existing);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return null;
    }

    internal static PaymentDetailDto ToDetailDto(
        PaymentDetailReadModel? detail,
        Payment payment,
        bool reused)
    {
        var dto = detail?.Adapt<PaymentDetailDto>() ?? payment.Adapt<PaymentDetailDto>();
        dto.Reused = reused;
        return dto;
    }
}
