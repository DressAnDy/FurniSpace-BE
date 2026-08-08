using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Catalog;

namespace FurniSpace.Application.Interfaces.Catalog;

public interface IProjectCatalogService
{
    Task<ServiceResult<ProjectCatalogListResponseDto>> GetProductsAsync(
        Guid projectId,
        Guid currentUserId,
        string? role,
        ProjectCatalogQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectCatalogProductDetailDto>> GetProductByIdAsync(
        Guid projectId,
        Guid productId,
        Guid currentUserId,
        string? role,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectCatalogProductVersionDetailDto>> GetProductVersionByIdAsync(
        Guid projectId,
        Guid productVersionId,
        Guid currentUserId,
        string? role,
        CancellationToken cancellationToken = default);
}
