using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Products;

namespace FurniSpace.Application.Interfaces.Products;

public interface IProductService
{
    Task<ServiceResult<ProductDto>> CreateAsync(
        CreateProductRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductDto>> UpdateAsync(
        Guid productId,
        UpdateProductRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductDetailDto>> GetByIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductListResponseDto>> GetAllAsync(
        int page,
        int limit,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductByCategoryResponseDto>> GetByCategoryAsync(
        Guid categoryId,
        int page,
        int limit,
        bool includeDefaultVersion,
        CancellationToken cancellationToken = default);
}
