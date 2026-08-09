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

    Task<AdminFinancialReceivablesSummaryReadModel> GetReceivablesSummaryAsync(
        AdminFinancialReceivablesQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminFinancialReceivableItemReadModel>> GetReceivableItemsAsync(
        AdminFinancialReceivablesQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<int> CountReceivableItemsAsync(
        AdminFinancialReceivablesQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminFinancialPaymentTypeBreakdownReadModel>> GetPaymentBreakdownAsync(
        DateTime fromUtc,
        DateTime toUtcExclusive,
        DateTime utcNow,
        string currency,
        IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminFinancialPaymentTypeAmountReadModel>> GetCollectedAmountsByPaymentTypeAsync(
        DateTime fromUtc,
        DateTime toUtcExclusive,
        string currency,
        IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminFinancialProjectRowReadModel>> GetProjectFinancialRowsAsync(
        AdminFinancialProjectsQueryReadModel query,
        DateTime utcNow,
        IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
        CancellationToken cancellationToken = default);

    Task<int> CountProjectFinancialRowsAsync(
        AdminFinancialProjectsQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<AdminFinancialProjectRowReadModel?> GetProjectFinancialRowAsync(
        Guid projectId,
        DateTime utcNow,
        IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
        CancellationToken cancellationToken = default);
}
