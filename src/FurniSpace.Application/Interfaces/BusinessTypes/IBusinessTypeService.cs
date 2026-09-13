using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.BusinessTypes;

namespace FurniSpace.Application.Interfaces.BusinessTypes;

public interface IBusinessTypeService
{
    Task<ServiceResult<BusinessTypeDto>> CreateAsync(
        CreateBusinessTypeRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<BusinessTypeDto>> UpdateAsync(
        int businessTypeId,
        UpdateBusinessTypeRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<BusinessTypeDto>> UpdateStatusAsync(
        int businessTypeId,
        UpdateBusinessTypeStatusRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<BusinessTypeListResponseDto>> GetAllAsync(
        BusinessTypeQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<BusinessTypeDto>> GetByIdAsync(
        int businessTypeId,
        CancellationToken cancellationToken = default);
}
