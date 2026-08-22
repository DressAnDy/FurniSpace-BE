using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.LayoutAssets;
using FurniSpace.Application.DTOs.Products;

namespace FurniSpace.Application.Interfaces.LayoutAssets;

public interface ILayoutAssetService
{
    Task<ServiceResult<LayoutAssetDto>> CreateAsync(
        CreateLayoutAssetRequestDto request,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<LayoutAssetDto>> UpdateAsync(
        Guid layoutAssetId,
        UpdateLayoutAssetRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<LayoutAssetDto>> UpdateStatusAsync(
        Guid layoutAssetId,
        UpdateLayoutAssetStatusRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<LayoutAssetListResponseDto>> GetAllAsync(
        LayoutAssetQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<LayoutAssetDto>> GetByIdAsync(
        Guid layoutAssetId,
        string? roleName,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<LayoutAssetListResponseDto>> GetRoomPlannerCatalogAsync(
        RoomPlannerLayoutAssetCatalogQueryDto query,
        string? roleName,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CatalogFileUploadResponseDto>> UploadFileAsync(
        Guid layoutAssetId,
        Guid currentUserId,
        UploadCatalogFileRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<LayoutAssetFileDto>>> GetFilesAsync(
        Guid layoutAssetId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<LayoutAssetFilePrimaryResponseDto>> SetPrimaryFileAsync(
        Guid layoutAssetId,
        Guid fileId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<LayoutAssetFileDto>> DeleteFileAsync(
        Guid layoutAssetId,
        Guid fileId,
        CancellationToken cancellationToken = default);
}
