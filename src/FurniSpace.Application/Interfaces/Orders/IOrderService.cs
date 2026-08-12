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

    Task<ServiceResult<OrderItemDeliveredQuantityDto>> UpdateDeliveredQuantityAsync(
        Guid orderItemId,
        Guid currentUserId,
        UpdateDeliveredQuantityRequestDto request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ServiceResult<OrderItemDeliveredQuantityDto>.Unauthorized());
    }

    Task<ServiceResult<OrderItemDeliveryConfirmationDto>> ConfirmItemDeliveryAsync(
        Guid orderItemId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ServiceResult<OrderItemDeliveryConfirmationDto>.Unauthorized());
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
}
