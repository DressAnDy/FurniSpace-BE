using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Storage;
using FurniSpace.Application.DTOs.Common;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Interfaces.Products;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Options;

namespace FurniSpace.Application.Services.Products;

public sealed class ProductPreviewImageService : IProductPreviewImageService
{
    private const string PreviewFileNotFoundMessage = "Product preview image not found.";

    private readonly IProductRepository _products;
    private readonly IProjectFileRepository _files;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _storage;
    private readonly CatalogDirectFileUploadService _catalogDirectUpload;
    private readonly ProductPreviewImageSettings _settings;
    private readonly FirebaseStorageSettings _firebaseSettings;

    public ProductPreviewImageService(
        IProductRepository products,
        IProjectFileRepository files,
        IFileStorageService storage,
        CatalogDirectFileUploadService catalogDirectUpload,
        IOptions<ProductPreviewImageSettings> settings,
        IOptions<FirebaseStorageSettings> firebaseSettings,
        IUnitOfWork unitOfWork)
    {
        _products = products;
        _files = files;
        _storage = storage;
        _catalogDirectUpload = catalogDirectUpload;
        _settings = settings.Value;
        _firebaseSettings = firebaseSettings.Value;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<PrepareDirectUploadResponseDto>> PreparePreviewUploadAsync(
        Guid productId,
        Guid currentUserId,
        UploadProductPreviewImageRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty)
        {
            return ServiceResult<PrepareDirectUploadResponseDto>.BadRequest(ProductValidationMessages.ProductIdRequired);
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<PrepareDirectUploadResponseDto>.Unauthorized("Authenticated account id is required.");
        }

        var validationError = ValidateUploadRequest(request);
        if (validationError is not null)
        {
            return ServiceResult<PrepareDirectUploadResponseDto>.Failure(validationError);
        }

        if (await ResolveProductErrorAsync<PrepareDirectUploadResponseDto>(productId, cancellationToken) is { } productError)
        {
            return productError;
        }

        var existingCount = await _files.CountProductPreviewFilesAsync(productId, cancellationToken);
        if (existingCount >= _settings.MaxCount)
        {
            return ServiceResult<PrepareDirectUploadResponseDto>.Failure(
                Error.Conflict(
                    ProductPreviewImageErrorCodes.MaxFilesExceeded,
                    $"A product can have at most {_settings.MaxCount} preview images."));
        }

        var existingLinks = await _files.GetProductPreviewFileLinkEntitiesAsync(productId, cancellationToken);
        var displayOrder = PreviewImageFileLinkOrdering.ResolveDisplayOrder(
            request.DisplayOrder,
            existingLinks,
            existingCount);

        var metadata = new UploadCatalogFileRequestDto
        {
            OriginalFileName = request.OriginalFileName,
            ContentType = request.ContentType,
            FileSizeBytes = request.FileSizeBytes,
            FileType = FileType.PRODUCT_PREVIEW,
            Description = request.Description,
            DisplayOrder = displayOrder
        };

        return await _catalogDirectUpload.PrepareAsync(
            new CatalogDirectUploadPrepareRequest(
                currentUserId,
                metadata,
                CatalogFileReferenceTypes.Product,
                productId,
                (_, generatedFileName) => CatalogPreviewImageServiceHelpers.BuildProductStorageObjectName(
                    _firebaseSettings,
                    productId,
                    generatedFileName),
                FileType.PRODUCT_PREVIEW,
                FileVisibility.CUSTOMER_VISIBLE,
                displayOrder),
            cancellationToken);
    }

    public async Task<ServiceResult<ProductPreviewImageUploadResponseDto>> CompletePreviewUploadAsync(
        Guid productId,
        Guid currentUserId,
        CompleteDirectUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty)
        {
            return ServiceResult<ProductPreviewImageUploadResponseDto>.BadRequest(ProductValidationMessages.ProductIdRequired);
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProductPreviewImageUploadResponseDto>.Unauthorized("Authenticated account id is required.");
        }

        if (await ResolveProductErrorAsync<ProductPreviewImageUploadResponseDto>(productId, cancellationToken) is { } productError)
        {
            return productError;
        }

        var existingCount = await _files.CountProductPreviewFilesAsync(productId, cancellationToken);
        var pendingLink = (await _files.GetFileLinkEntitiesByFileIdAsync(request.FileId, cancellationToken))
            .FirstOrDefault(link =>
                link.ReferenceType == CatalogFileReferenceTypes.Product &&
                link.ReferenceId == productId);
        var shiftDisplayOrders = pendingLink?.DisplayOrder is int insertOrder && insertOrder <= existingCount;

        var completeResult = await _catalogDirectUpload.CompleteAsync(
            new CatalogDirectUploadCompleteRequest(
                currentUserId,
                request.FileId,
                CatalogFileReferenceTypes.Product,
                productId,
                "Product preview image uploaded successfully."),
            async (context, ct) =>
            {
                if (shiftDisplayOrders)
                {
                    var existingLinks = (await _files.GetProductPreviewFileLinkEntitiesAsync(productId, ct))
                        .Where(link => link.FileId != context.FileLink.FileId)
                        .ToList();
                    PreviewImageFileLinkOrdering.ShiftDisplayOrdersForInsert(
                        existingLinks,
                        context.FileLink.DisplayOrder!.Value);
                }

                var pendingLinks = PreviewImageFileLinkOrdering.MergePendingPreviewLink(
                    await _files.GetProductPreviewFileLinkEntitiesAsync(productId, ct),
                    context.FileLink);
                PreviewImageFileLinkOrdering.NormalizeDisplayOrdersAndPrimary(pendingLinks);
                PreviewImageFileLinkOrdering.EnsureUniquePositiveDisplayOrders(pendingLinks);
            },
            cancellationToken);

        if (completeResult.Status is not (200 or 201) || completeResult.Data is null)
        {
            return new ServiceResult<ProductPreviewImageUploadResponseDto>(
                completeResult.Status,
                completeResult.Message ?? "Product preview image upload completion failed.")
            {
                ErrorCode = completeResult.ErrorCode,
                Errors = completeResult.Errors
            };
        }

        var uploaded = completeResult.Data;
        return ServiceResult<ProductPreviewImageUploadResponseDto>.Created(
            new ProductPreviewImageUploadResponseDto
            {
                FileId = uploaded.FileId,
                Url = uploaded.FileUrl,
                DisplayOrder = uploaded.DisplayOrder ?? 0,
                FileType = uploaded.FileType,
                Description = uploaded.Description,
                CreatedAt = uploaded.CreatedAt ?? uploaded.UploadedAt
            },
            completeResult.Message ?? "Product preview image uploaded successfully.");
    }

    public async Task<ServiceResult<ProductPreviewImageListResponseDto>> GetListAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        if (await ResolveProductErrorAsync<ProductPreviewImageListResponseDto>(productId, cancellationToken) is { } productError)
        {
            return productError;
        }

        return await BuildListSuccessAsync(
            productId,
            "Product preview images retrieved successfully.",
            cancellationToken);
    }

    public async Task<ServiceResult<IReadOnlyList<ProductPreviewReorderItemDto>>> ReorderAsync(
        Guid productId,
        ReorderProductPreviewImagesRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (await ResolveProductErrorAsync<IReadOnlyList<ProductPreviewReorderItemDto>>(productId, cancellationToken) is { } productError)
        {
            return productError;
        }

        var fileLinks = (await _files.GetProductPreviewFileLinkEntitiesAsync(productId, cancellationToken)).ToList();
        var expectedIds = fileLinks.Select(link => link.FileId).ToHashSet();
        var fileIds = request.FileIds ?? [];

        if (fileLinks.Count == 0 && fileIds.Count == 0)
        {
            return ServiceResult<IReadOnlyList<ProductPreviewReorderItemDto>>.Success(
                Array.Empty<ProductPreviewReorderItemDto>(),
                "Product preview images reordered successfully.");
        }

        if (fileIds.Count != fileIds.Distinct().Count())
        {
            return ServiceResult<IReadOnlyList<ProductPreviewReorderItemDto>>.Failure(
                Error.BadRequest(
                    ProductPreviewImageErrorCodes.DuplicateFileId,
                    "fileIds must not contain duplicates."));
        }

        var foreignFileError = await ValidateForeignProductPreviewFileIdsAsync(
            productId,
            fileIds,
            expectedIds,
            cancellationToken);
        if (foreignFileError is not null)
        {
            return ServiceResult<IReadOnlyList<ProductPreviewReorderItemDto>>.Failure(foreignFileError);
        }

        if (!PreviewImageFileLinkOrdering.TryBuildExactReorderMap(
                fileIds,
                expectedIds,
                out _,
                out var validationMessage))
        {
            return ServiceResult<IReadOnlyList<ProductPreviewReorderItemDto>>.Failure(
                Error.BadRequest(
                    ProductPreviewImageErrorCodes.InvalidReorderPayload,
                    validationMessage!));
        }

        await UnitOfWorkTransactions.ExecuteAsync(
            _unitOfWork,
            async ct =>
            {
                PreviewImageFileLinkOrdering.ApplyReorderFromFileIds(fileIds, fileLinks);
                PreviewImageFileLinkOrdering.EnsureUniquePositiveDisplayOrders(fileLinks);
                await _unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);

        return ServiceResult<IReadOnlyList<ProductPreviewReorderItemDto>>.Success(
            PreviewImageFileLinkOrdering.MapProductPreviewReorderItems(fileLinks),
            "Product preview images reordered successfully.");
    }

    private async Task<Error?> ValidateForeignProductPreviewFileIdsAsync(
        Guid productId,
        IReadOnlyList<Guid> fileIds,
        HashSet<Guid> expectedIds,
        CancellationToken cancellationToken)
    {
        foreach (var fileId in fileIds.Where(id => !expectedIds.Contains(id)))
        {
            var links = await _files.GetFileLinkEntitiesByFileIdAsync(fileId, cancellationToken);
            var belongsToOtherProduct = links.Any(link =>
                link.FileType == FileType.PRODUCT_PREVIEW &&
                link.ReferenceType == CatalogFileReferenceTypes.Product &&
                link.ReferenceId != productId);

            if (belongsToOtherProduct)
            {
                return Error.BadRequest(
                    ProductPreviewImageErrorCodes.FileNotBelongToProduct,
                    "One or more file IDs do not belong to the specified product.");
            }

            if (links.Count == 0 && await _files.GetByIdAsync(fileId, cancellationToken) is null)
            {
                return Error.NotFound(
                    ProductPreviewImageErrorCodes.FileNotFound,
                    "One or more file IDs were not found.");
            }
        }

        return null;
    }

    public async Task<ServiceResult<DeleteProductPreviewImageResponseDto>> DeleteAsync(
        Guid productId,
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty)
        {
            return ServiceResult<DeleteProductPreviewImageResponseDto>.BadRequest(ProductValidationMessages.ProductIdRequired);
        }

        if (fileId == Guid.Empty)
        {
            return ServiceResult<DeleteProductPreviewImageResponseDto>.BadRequest("File id is required.");
        }

        if (await ResolveProductErrorAsync<DeleteProductPreviewImageResponseDto>(productId, cancellationToken) is { } productError)
        {
            return productError;
        }

        var deleteValidationError = await ValidateProductPreviewDeleteAsync(productId, fileId, cancellationToken);
        if (deleteValidationError is not null)
        {
            return ServiceResult<DeleteProductPreviewImageResponseDto>.Failure(deleteValidationError);
        }

        var file = await _files.GetByIdAsync(fileId, cancellationToken);
        if (file is null)
        {
            return ServiceResult<DeleteProductPreviewImageResponseDto>.Failure(
                Error.NotFound(
                    ProductPreviewImageErrorCodes.FileNotFound,
                    PreviewFileNotFoundMessage));
        }

        var fileLinks = await _files.GetFileLinkEntitiesByFileIdAsync(fileId, cancellationToken);
        var remainingLinks = (await _files.GetProductPreviewFileLinkEntitiesAsync(productId, cancellationToken))
            .Where(link => link.FileId != fileId)
            .OrderBy(link => link.DisplayOrder ?? int.MaxValue)
            .ThenBy(link => link.CreatedAt ?? DateTime.MinValue)
            .ToList();
        var remainingCount = remainingLinks.Count;
        var reindexed = remainingCount > 0;
        var storagePath = file.StoragePath;

        await UnitOfWorkTransactions.ExecuteAsync(
            _unitOfWork,
            async ct =>
            {
                _files.RemoveFileLinks(fileLinks);
                _files.Remove(file);

                if (reindexed)
                {
                    PreviewImageFileLinkOrdering.NormalizeDisplayOrdersAndPrimary(remainingLinks);
                    PreviewImageFileLinkOrdering.EnsureUniquePositiveDisplayOrders(remainingLinks);
                }

                await _unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);

        await _storage.DeleteAsync(storagePath, cancellationToken);

        return ServiceResult<DeleteProductPreviewImageResponseDto>.Success(
            new DeleteProductPreviewImageResponseDto
            {
                DeletedFileId = fileId,
                RemainingCount = remainingCount,
                Reindexed = reindexed
            },
            "Product preview image deleted successfully.");
    }

    private async Task<Error?> ValidateProductPreviewDeleteAsync(
        Guid productId,
        Guid fileId,
        CancellationToken cancellationToken)
    {
        if (await _files.GetProductPreviewFileAsync(productId, fileId, cancellationToken) is not null)
        {
            return null;
        }

        var fileLinks = await _files.GetFileLinkEntitiesByFileIdAsync(fileId, cancellationToken);
        if (fileLinks.Any(link =>
                link.FileType == FileType.PRODUCT_PREVIEW &&
                link.ReferenceType == CatalogFileReferenceTypes.Product &&
                link.ReferenceId != productId))
        {
            return Error.BadRequest(
                ProductPreviewImageErrorCodes.FileNotBelongToProduct,
                "The file does not belong to the specified product.");
        }

        if (fileLinks.Any(link =>
                link.ReferenceType == CatalogFileReferenceTypes.Product &&
                link.ReferenceId == productId &&
                link.FileType != FileType.PRODUCT_PREVIEW))
        {
            return Error.BadRequest(
                ProductPreviewImageErrorCodes.InvalidFileType,
                "Only product preview images can be deleted via this endpoint.");
        }

        if (await _files.GetByIdAsync(fileId, cancellationToken) is null)
        {
            return Error.NotFound(
                ProductPreviewImageErrorCodes.FileNotFound,
                PreviewFileNotFoundMessage);
        }

        return Error.NotFound(
            ProductPreviewImageErrorCodes.PreviewFileNotFound,
            PreviewFileNotFoundMessage);
    }

    private async Task<ServiceResult<T>?> ResolveProductErrorAsync<T>(
        Guid productId,
        CancellationToken cancellationToken)
    {
        if (productId == Guid.Empty)
        {
            return ServiceResult<T>.BadRequest(ProductValidationMessages.ProductIdRequired);
        }

        if (await _products.GetByIdAsync(productId, cancellationToken) is null)
        {
            return ServiceResult<T>.Failure(
                Error.NotFound(
                    ProductPreviewImageErrorCodes.ProductNotFound,
                    ProductValidationMessages.ProductNotFound));
        }

        return null;
    }

    private async Task<ServiceResult<ProductPreviewImageListResponseDto>> BuildListSuccessAsync(
        Guid productId,
        string message,
        CancellationToken cancellationToken)
    {
        var previews = await _files.GetProductPreviewFilesAsync(productId, cancellationToken);
        return ServiceResult<ProductPreviewImageListResponseDto>.Success(
            new ProductPreviewImageListResponseDto
            {
                ProductId = productId,
                Items = MapPreviewItems(previews)
            },
            message);
    }

    private Error? ValidateUploadRequest(UploadProductPreviewImageRequestDto request)
    {
        var validationError = CatalogPreviewUploadValidation.ValidateMetadata(
            request,
            _settings,
            ProductPreviewImageErrorCodes.InvalidFileType,
            ProductPreviewImageErrorCodes.FileTooLarge);
        if (validationError is not null)
        {
            return validationError;
        }

        return CatalogPreviewUploadValidation.ValidateDisplayOrderInRange(
            request.DisplayOrder,
            _settings.MaxCount,
            ProductPreviewImageErrorCodes.InvalidFileType);
    }

    private static List<ProductPreviewImageDto> MapPreviewItems(
        IReadOnlyList<Infrastructure.ReadModels.Products.ProductPreviewImageReadModel> previews)
    {
        var ordered = previews
            .OrderBy(preview => preview.DisplayOrder <= 0 ? int.MaxValue : preview.DisplayOrder)
            .ThenBy(preview => preview.CreatedAt)
            .ToList();

        return ordered
            .Select((preview, index) => new ProductPreviewImageDto
            {
                FileId = preview.FileId,
                Url = preview.FileUrl,
                DisplayOrder = index + 1,
                FileType = preview.FileType,
                Description = preview.Description,
                MimeType = preview.MimeType,
                FileSizeBytes = preview.FileSizeBytes,
                IsCover = index == 0,
                CreatedAt = preview.CreatedAt
            })
            .ToList();
    }
}
