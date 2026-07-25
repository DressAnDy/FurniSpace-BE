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

    Task<ServiceResult<OrderDetailDto>> UpdateFinancialAdjustmentAsync(
        Guid orderId,
        Guid currentUserId,
        UpdateOrderFinancialAdjustmentRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OrderAdjustmentDto>> CreateAdjustmentAsync(
        Guid orderId,
        Guid currentUserId,
        CreateOrderAdjustmentDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OrderAdjustmentItemDto>> AddAdjustmentItemAsync(
        Guid orderAdjustmentId,
        Guid currentUserId,
        UpsertOrderAdjustmentItemDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OrderAdjustmentItemDto>> UpdateAdjustmentItemAsync(
        Guid orderAdjustmentItemId,
        Guid currentUserId,
        UpsertOrderAdjustmentItemDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OrderAdjustmentDto>> DeleteAdjustmentItemAsync(
        Guid orderAdjustmentItemId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OrderAdjustmentConfirmationDto>> ConfirmAdjustmentAsync(
        Guid orderAdjustmentId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ServiceResult<OrderAdjustmentConfirmationDto>.Unauthorized());
    }

    Task<ServiceResult<OrderDeliveryStartDto>> StartDeliveryAsync(
        Guid orderId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ServiceResult<OrderDeliveryStartDto>.Unauthorized());
    }
}
