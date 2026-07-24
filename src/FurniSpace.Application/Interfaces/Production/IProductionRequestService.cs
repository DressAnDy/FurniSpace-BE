#nullable enable

using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Production;

namespace FurniSpace.Application.Interfaces.Production;

public interface IProductionRequestService
{
    Task<ServiceResult<ProductionRequestCreatedDto>> CreateAsync(
        Guid orderId,
        Guid currentUserId,
        CreateProductionRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<List<AvailableProductionStaffDto>>> GetAvailableStaffAsync(
        Guid currentUserId,
        AvailableProductionStaffQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductionRequestAssignmentDto>> AssignAsync(
        Guid productionRequestId,
        Guid currentUserId,
        AssignProductionRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductionRequestListResponseDto>> GetQueueAsync(
        Guid currentUserId,
        ProductionRequestQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductionRequestDetailDto>> GetDetailAsync(
        Guid productionRequestId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}
