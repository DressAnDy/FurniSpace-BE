using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.BusinessTypes;

namespace FurniSpace.Application.Interfaces.BusinessTypes;

public interface IBusinessTypeService
{
    Task<ServiceResult<BusinessTypeListResponseDto>> GetAllAsync(
        BusinessTypeQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<BusinessTypeDto>> GetByIdAsync(
        int businessTypeId,
        CancellationToken cancellationToken = default);
}
