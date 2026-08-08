using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Catalog;
using FurniSpace.Application.DTOs.ProductVersions;
using FurniSpace.Application.DTOs.Products;

namespace FurniSpace.Application.Interfaces.ProductVersions;

public interface IProductVersionService
{
    Task<ServiceResult<ProductVersionDto>> CreateAsync(
        Guid productId,
        CreateProductVersionRequestDto request,
        bool allowTaxConfiguration = false,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductVersionDto>> UpdateAsync(
        Guid productVersionId,
        UpdateProductVersionRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<SetDefaultProductVersionDto>> SetDefaultAsync(
        Guid productVersionId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductVersionDetailDto>> GetByIdAsync(
        Guid productVersionId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CatalogFileUploadResponseDto>> UploadFileAsync(
        Guid productVersionId,
        Guid currentUserId,
        UploadCatalogFileRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<ProductVersionPreviewReorderItemDto>>> ReorderPreviewFilesAsync(
        Guid productVersionId,
        ReorderProductVersionPreviewFilesRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<DeleteProductVersionPreviewImageResponseDto>> DeletePreviewFileAsync(
        Guid productVersionId,
        Guid fileId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductVersionListResponseDto>> GetListByProductAsync(
        Guid productId,
        ProductVersionListQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductVersionLifecycleStatusResponseDto>> ActivateAsync(
        Guid productVersionId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductVersionLifecycleStatusResponseDto>> DeactivateAsync(
        Guid productVersionId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductVersionLifecycleStatusResponseDto>> ArchiveAsync(
        Guid productVersionId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductVersionLifecycleStatusResponseDto>> RestoreAsync(
        Guid productVersionId,
        CancellationToken cancellationToken = default);
}
