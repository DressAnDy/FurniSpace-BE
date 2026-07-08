using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Payments;

namespace FurniSpace.Application.Interfaces.Payments;

public interface IPaymentService
{
    Task<ServiceResult<PaymentDetailDto>> CreateTestPaymentAsync(
        Guid currentUserId,
        CreateTestPaymentRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentDetailDto>> GetByIdAsync(
        Guid paymentId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentListResponseDto>> GetListAsync(
        Guid currentUserId,
        PaymentQueryDto query,
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

    Task<ServiceResult<PayOsConfirmWebhookResponseDto>> ConfirmPayOsWebhookAsync(
        PayOsConfirmWebhookRequestDto request,
        CancellationToken cancellationToken = default);
}
