using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Catalog;

namespace FurniSpace.Application.Interfaces.Catalog;

public interface IAdminCatalogService
{
    Task<ServiceResult<AdminCatalogListResponseDto>> GetProductsAsync(
        AdminCatalogQueryDto query,
        CancellationToken cancellationToken = default);
}
