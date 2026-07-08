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
}
