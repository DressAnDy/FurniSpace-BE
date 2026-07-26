using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.DTOs.Payments;

namespace FurniSpace.Application.Interfaces.Payments;

public interface IPaymentService
{
    Task<ServiceResult<PaymentDetailDto>> CreateTestPaymentAsync(
        Guid currentUserId,
        CreateTestPaymentRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentDetailDto>> CreateDepositPaymentForOrderAsync(
        Guid orderId,
        Guid currentUserId,
        CreateOrderDepositPaymentRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentDetailDto>> CreateRemainingPaymentForOrderAsync(
        Guid orderId,
        Guid currentUserId,
        CreateOrderRemainingPaymentRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentDetailDto>> CreateProjectStartFeePaymentAsync(
        Guid projectId,
        Guid currentUserId,
        CreateProjectStartFeePaymentRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectStartFeeStatusDto>> GetProjectStartFeeStatusAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentDetailDto>> GetByIdAsync(
        Guid paymentId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentListResponseDto>> GetListAsync(
        Guid currentUserId,
        PaymentQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentSummaryResponseDto>> GetSummaryAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentTransactionListResponseDto>> GetTransactionsAsync(
        Guid paymentId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentStatusByCodeDto>> GetStatusByCodeAsync(
        string paymentCode,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<SePayVietQrResponseDto>> GenerateSePayVietQrAsync(
        Guid paymentId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<bool> CanAccessPaymentAsync(
        Guid paymentId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PayOsPaymentLinkResponseDto>> CreatePayOsPaymentLinkAsync(
        Guid paymentId,
        Guid currentUserId,
        CreatePayOsPaymentLinkRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentTransactionAttemptResponseDto>> CreatePaymentTransactionAttemptAsync(
        Guid paymentId,
        Guid currentUserId,
        CreatePaymentTransactionAttemptRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentTransactionDto?>> GetActiveTransactionAsync(
        Guid paymentId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentTransactionDto>> CancelTransactionAsync(
        Guid paymentId,
        Guid paymentTransactionId,
        Guid currentUserId,
        CancelPaymentTransactionRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PayOsConfirmWebhookResponseDto>> ConfirmPayOsWebhookAsync(
        PayOsConfirmWebhookRequestDto request,
        CancellationToken cancellationToken = default);
}
