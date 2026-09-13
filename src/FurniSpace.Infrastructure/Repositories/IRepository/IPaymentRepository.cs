using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Payments;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken = default);
    Task<PaymentDetailReadModel?> GetDetailAsync(Guid paymentId, CancellationToken cancellationToken = default);
    Task<PaymentDetailReadModel?> GetDetailByPaymentCodeAsync(string paymentCode, CancellationToken cancellationToken = default);
    Task<PaymentStatusByCodeReadModel?> GetStatusByPaymentCodeAsync(string paymentCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentListItemReadModel>> GetListAsync(PaymentQueryReadModel query, CancellationToken cancellationToken = default);
    Task<int> CountAsync(PaymentQueryReadModel query, CancellationToken cancellationToken = default);
    Task<PaymentSummaryReadModel> GetSummaryAsync(PaymentQueryReadModel query, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Payment>> GetExpiredPaymentsForSyncAsync(PaymentQueryReadModel query, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentListItemReadModel>> GetListByOrderIdAsync(
        Guid orderId,
        PaymentStatus? status = null,
        PaymentType? paymentType = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<PaymentListItemReadModel>>([]);
    }

    Task<IReadOnlyList<PaymentTransactionReadModel>> GetTransactionsByPaymentIdsAsync(
        IReadOnlyCollection<Guid> paymentIds,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<PaymentTransactionReadModel>>([]);
    }

    Task<IReadOnlyList<PaymentTransactionReadModel>> GetTransactionsByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default);
    Task<PaymentTransaction?> GetTransactionByIdAsync(Guid paymentTransactionId, CancellationToken cancellationToken = default);
    Task<PaymentTransactionReadModel?> GetLatestPendingTransactionAsync(
        Guid paymentId,
        PaymentProvider provider,
        PaymentMethod method,
        CancellationToken cancellationToken = default);
    Task<PaymentTransactionReadModel?> GetLatestTransactionAsync(Guid paymentId, CancellationToken cancellationToken = default);
    Task<IReadOnlySet<Guid>> GetPaymentIdsWithSuccessfulTransactionAsync(
        IReadOnlyCollection<Guid> paymentIds,
        CancellationToken cancellationToken = default);
    Task<bool> PaymentCodeExistsAsync(string paymentCode, CancellationToken cancellationToken = default);
    Task<bool> TransactionCodeExistsAsync(string transactionCode, CancellationToken cancellationToken = default);
    Task<bool> ProviderTransactionExistsAsync(PaymentProvider provider, string providerTransactionId, CancellationToken cancellationToken = default);
    Task<bool> PayOsOrderCodeExistsAsync(string orderCode, CancellationToken cancellationToken = default);
    Task<PaymentTransaction?> GetTransactionByProviderReferenceAsync(
        PaymentProvider provider,
        string providerReferenceCode,
        CancellationToken cancellationToken = default);
    Task AddPaymentAsync(Payment payment, CancellationToken cancellationToken = default);
    Task AddTransactionAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default);
    Task<Payment?> GetByOrderAndTypeAsync(
        Guid orderId,
        PaymentType paymentType,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Payment>> GetAllByOrderAndTypeAsync(
        Guid orderId,
        PaymentType paymentType,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Payment>>([]);
    }

    Task<IReadOnlyList<PaymentTransaction>> GetTransactionEntitiesByPaymentIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<PaymentTransaction>>([]);
    }
    Task<Payment?> GetByProjectAndTypeAsync(
        Guid projectId,
        PaymentType paymentType,
        CancellationToken cancellationToken = default);
    Task<decimal> SumOrderScopedPaidAmountAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
    Task<bool> HasSuccessfulTransactionAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default);
    void UpdatePayment(Payment payment);
    void UpdateTransaction(PaymentTransaction transaction);
}
