using FurniSpace.Infrastructure.ReadModels.Financial;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IFinancialReadRepository
{
    Task<AdminFinancialSummaryReadModel> GetAdminSummaryAsync(
        DateTime fromUtc,
        DateTime toUtcExclusive,
        DateTime utcNow,
        string currency,
        IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
        CancellationToken cancellationToken = default);
}
