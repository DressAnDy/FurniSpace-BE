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
        int[]? businessTypeIds = null,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductListResponseDto>> SearchAsync(
        ProductSearchRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductSuggestResponseDto>> SuggestAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductListResponseDto>> GetSimilarAsync(
        Guid productId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductByCategoryResponseDto>> GetByCategoryAsync(
        Guid categoryId,
        int page,
        int limit,
        bool includeDefaultVersion,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CatalogFileUploadResponseDto>> UploadFileAsync(
        Guid productId,
        Guid currentUserId,
        UploadCatalogFileRequestDto request,
        CancellationToken cancellationToken = default);
}
