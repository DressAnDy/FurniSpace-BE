using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProductVersions;

namespace FurniSpace.Application.Interfaces.ProductVersions;

public interface IProductVersionService
{
    Task<ServiceResult<ProductVersionDto>> CreateAsync(
        Guid productId,
        CreateProductVersionRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductVersionDto>> UpdateAsync(
        Guid productVersionId,
        UpdateProductVersionRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<SetDefaultProductVersionDto>> SetDefaultAsync(
        Guid productVersionId,
        CancellationToken cancellationToken = default);
}
