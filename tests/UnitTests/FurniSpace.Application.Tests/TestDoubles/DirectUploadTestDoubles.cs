using System;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Storage;
using FurniSpace.Application.DTOs.Common;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.DTOs.ProjectFiles;
using FurniSpace.Application.Interfaces.LayoutAssets;
using FurniSpace.Application.Interfaces.Products;
using FurniSpace.Application.Interfaces.ProductVersions;
using FurniSpace.Application.Interfaces.ProjectFiles;
using FurniSpace.Application.Services.ProjectFiles;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;

namespace FurniSpace.Application.Tests.TestDoubles;

internal static class DirectUploadTestDoubles
{
    internal static CompleteDirectUploadRequestDto CompleteRequest(Guid fileId) =>
        new() { FileId = fileId };

    internal static CompleteProjectFileUploadRequestDto CompleteProjectRequest(Guid fileId) =>
        new() { FileId = fileId };

    internal static DirectFileUploadCoordinator CreateCoordinator(
        IFileStorageService storage,
        FirebaseStorageSettings? firebaseSettings = null)
    {
        var settings = firebaseSettings ?? new FirebaseStorageSettings
        {
            Bucket = "test-bucket",
            ProjectFilesPrefix = "projects",
            UploadSignedUrlExpirationMinutes = 15
        };

        var directUploadStorage = storage as IDirectFileUploadStorageService
            ?? new NoOpDirectUploadStorageService();

        return new DirectFileUploadCoordinator(
            storage,
            directUploadStorage,
            settings);
    }

    internal static async Task<ServiceResult<CatalogFileUploadResponseDto>> CompleteProductFileUploadAsync(
        IProductService service,
        Guid productId,
        Guid userId,
        UploadCatalogFileRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var prepareResult = await service.PrepareFileUploadAsync(productId, userId, request, cancellationToken);
        if (prepareResult.Status is not (200 or 201) || prepareResult.Data is null)
        {
            return FromPrepareFailure<PrepareDirectUploadResponseDto, CatalogFileUploadResponseDto>(prepareResult);
        }

        return await service.CompleteFileUploadAsync(
            productId,
            userId,
            CompleteRequest(prepareResult.Data.FileId),
            cancellationToken);
    }

    internal static async Task<ServiceResult<CatalogFileUploadResponseDto>> CompleteProductVersionFileUploadAsync(
        IProductVersionService service,
        Guid productVersionId,
        Guid userId,
        UploadCatalogFileRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var prepareResult = await service.PrepareFileUploadAsync(productVersionId, userId, request, cancellationToken);
        if (prepareResult.Status is not (200 or 201) || prepareResult.Data is null)
        {
            return FromPrepareFailure<PrepareDirectUploadResponseDto, CatalogFileUploadResponseDto>(prepareResult);
        }

        return await service.CompleteFileUploadAsync(
            productVersionId,
            userId,
            CompleteRequest(prepareResult.Data.FileId),
            cancellationToken);
    }

    internal static async Task<ServiceResult<CatalogFileUploadResponseDto>> CompleteLayoutAssetFileUploadAsync(
        ILayoutAssetService service,
        Guid layoutAssetId,
        Guid userId,
        UploadCatalogFileRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var prepareResult = await service.PrepareFileUploadAsync(layoutAssetId, userId, request, cancellationToken);
        if (prepareResult.Status is not (200 or 201) || prepareResult.Data is null)
        {
            return FromPrepareFailure<PrepareDirectUploadResponseDto, CatalogFileUploadResponseDto>(prepareResult);
        }

        return await service.CompleteFileUploadAsync(
            layoutAssetId,
            userId,
            CompleteRequest(prepareResult.Data.FileId),
            cancellationToken);
    }

    internal static async Task<ServiceResult<ProductPreviewImageUploadResponseDto>> CompleteProductPreviewUploadAsync(
        IProductPreviewImageService service,
        Guid productId,
        Guid userId,
        UploadProductPreviewImageRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var prepareResult = await service.PreparePreviewUploadAsync(productId, userId, request, cancellationToken);
        if (prepareResult.Status is not (200 or 201) || prepareResult.Data is null)
        {
            return FromPrepareFailure<PrepareDirectUploadResponseDto, ProductPreviewImageUploadResponseDto>(prepareResult);
        }

        return await service.CompletePreviewUploadAsync(
            productId,
            userId,
            CompleteRequest(prepareResult.Data.FileId),
            cancellationToken);
    }

    internal static async Task<ServiceResult<ProjectFileUploadResponseDto>> CompleteProjectFileUploadAsync(
        ProjectFileService service,
        Guid projectId,
        Guid userId,
        PrepareProjectFileUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var prepareResult = await service.PrepareProjectFileUploadAsync(projectId, userId, request, cancellationToken);
        if (prepareResult.Status is not (200 or 201) || prepareResult.Data is null)
        {
            return FromPrepareFailure<PrepareProjectFileUploadResponseDto, ProjectFileUploadResponseDto>(prepareResult);
        }

        return await service.CompleteProjectFileUploadAsync(
            projectId,
            userId,
            CompleteProjectRequest(prepareResult.Data.FileId),
            cancellationToken);
    }

    internal static async Task<ServiceResult<ProjectFileUploadResponseDto>> CompleteProjectAreaFileUploadAsync(
        ProjectFileService service,
        Guid projectAreaId,
        Guid userId,
        PrepareProjectFileUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var prepareResult = await service.PrepareProjectAreaFileUploadAsync(projectAreaId, userId, request, cancellationToken);
        if (prepareResult.Status is not (200 or 201) || prepareResult.Data is null)
        {
            return FromPrepareFailure<PrepareProjectAreaFileUploadResponseDto, ProjectFileUploadResponseDto>(prepareResult);
        }

        return await service.CompleteProjectAreaFileUploadAsync(
            projectAreaId,
            userId,
            CompleteProjectRequest(prepareResult.Data.FileId),
            cancellationToken);
    }

    internal static ServiceResult<TComplete> FromPrepareFailure<TPrepare, TComplete>(
        ServiceResult<TPrepare> prepare)
    {
        return new ServiceResult<TComplete>
        {
            Status = prepare.Status,
            Message = prepare.Message,
            Errors = prepare.Errors,
            ErrorCode = prepare.ErrorCode
        };
    }

    internal sealed class NoOpDirectUploadStorageService : IDirectFileUploadStorageService
    {
        public Task<StorageSignedUploadResult> CreateSignedUploadUrlAsync(
            StorageSignedUploadRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new StorageSignedUploadResult
            {
                UploadUrl = "https://storage.example.com/upload",
                ContentType = request.ContentType,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15)
            });

        public Task<StorageUploadResult> FinalizeDirectUploadAsync(
            StorageDirectUploadFinalizeRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new StorageUploadResult
            {
                ObjectName = request.ObjectName,
                PublicUrl = $"https://storage.example.com/{request.ObjectName}",
                Bucket = "test-bucket"
            });
    }
}
