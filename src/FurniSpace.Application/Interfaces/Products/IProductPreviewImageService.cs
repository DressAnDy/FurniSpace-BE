using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Products;

namespace FurniSpace.Application.Interfaces.Products;

public interface IProductPreviewImageService
{
    Task<ServiceResult<ProductPreviewImageUploadResponseDto>> UploadAsync(
        Guid productId,
        Guid currentUserId,
        UploadProductPreviewImageRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductPreviewImageListResponseDto>> GetListAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<ProductPreviewReorderItemDto>>> ReorderAsync(
        Guid productId,
        ReorderProductPreviewImagesRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<DeleteProductPreviewImageResponseDto>> DeleteAsync(
        Guid productId,
        Guid fileId,
        CancellationToken cancellationToken = default);
}
