using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Orders;

namespace FurniSpace.Application.Interfaces.Orders;

public interface IOrderService
{
    Task<ServiceResult<OrderListResponseDto>> GetByProjectAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OrderDetailDto>> GetDetailAsync(
        Guid orderId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OrderDeliveryStartDto>> StartDeliveryAsync(
        Guid orderId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ServiceResult<OrderDeliveryStartDto>.Unauthorized());
    }

    Task<ServiceResult<OrderDeliveryCompletionDto>> CompleteDeliveryAsync(
        Guid orderId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ServiceResult<OrderDeliveryCompletionDto>.Unauthorized());
    }

    Task<ServiceResult<OrderDeliveryConfirmationDto>> ConfirmDeliveryAsync(
        Guid orderId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ServiceResult<OrderDeliveryConfirmationDto>.Unauthorized());
    }

    Task<ServiceResult<OrderFinalPaymentPreparationDto>> PrepareFinalPaymentAsync(
        Guid orderId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ServiceResult<OrderFinalPaymentPreparationDto>.Unauthorized());
    }

    Task<ServiceResult<OrderCompletionDto>> CompleteAsync(
        Guid orderId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ServiceResult<OrderCompletionDto>.Unauthorized());
    }

    Task<ServiceResult<DeliveryDetailDto>> CreateDeliveryBatchAsync(
        Guid orderId,
        Guid currentUserId,
        CreateDeliveryBatchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ServiceResult<DeliveryDetailDto>.Unauthorized());
    }

    Task<ServiceResult<DeliveryListResponseDto>> GetDeliveriesAsync(
        Guid orderId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ServiceResult<DeliveryListResponseDto>.Unauthorized());
    }

    Task<ServiceResult<DeliveryDetailDto>> GetDeliveryDetailAsync(
        Guid orderId,
        Guid deliveryId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ServiceResult<DeliveryDetailDto>.Unauthorized());
    }

    Task<ServiceResult<DeliveryBatchCompletionDto>> CompleteDeliveryBatchAsync(
        Guid orderId,
        Guid deliveryId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ServiceResult<DeliveryBatchCompletionDto>.Unauthorized());
    }

    Task<ServiceResult<OrderDeliveryTrackingDto>> GetDeliveryTrackingAsync(
        Guid orderId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ServiceResult<OrderDeliveryTrackingDto>.Unauthorized());
    }
}
