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
    Task<IReadOnlyList<PaymentTransactionReadModel>> GetTransactionsByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default);
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
    Task<Payment?> GetByProjectAndTypeAsync(
        Guid projectId,
        PaymentType paymentType,
        CancellationToken cancellationToken = default);
    Task<decimal> SumOrderScopedPaidAmountAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
    void UpdatePayment(Payment payment);
    void UpdateTransaction(PaymentTransaction transaction);
}
