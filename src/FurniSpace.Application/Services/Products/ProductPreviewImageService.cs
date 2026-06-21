using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Interfaces.Products;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace FurniSpace.Application.Services.Products;

public sealed class ProductPreviewImageService : IProductPreviewImageService
{
    private readonly IProductRepository _products;
    private readonly IProjectFileRepository _files;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _storage;
    private readonly ProductPreviewImageSettings _settings;
    private readonly FirebaseStorageSettings _firebaseSettings;

    public ProductPreviewImageService(
        IProductRepository products,
        IProjectFileRepository files,
        IFileStorageService storage,
        IOptions<ProductPreviewImageSettings> settings,
        IOptions<FirebaseStorageSettings> firebaseSettings,
        IUnitOfWork unitOfWork)
    {
        _products = products;
        _files = files;
        _storage = storage;
        _settings = settings.Value;
        _firebaseSettings = firebaseSettings.Value;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<ProductPreviewImageUploadResponseDto>> UploadAsync(
        Guid productId,
        Guid currentUserId,
        UploadProductPreviewImageRequestDto request,
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

        var validationError = ValidateUploadRequest(request);
        if (validationError is not null)
        {
            return ServiceResult<ProductPreviewImageUploadResponseDto>.Failure(validationError);
        }

        if (await _products.GetByIdAsync(productId, cancellationToken) is null)
        {
            return ServiceResult<ProductPreviewImageUploadResponseDto>.NotFound(ProductValidationMessages.ProductNotFound);
        }

        var existingCount = await _files.CountProductPreviewFilesAsync(productId, cancellationToken);
        if (existingCount >= _settings.MaxCount)
        {
            return ServiceResult<ProductPreviewImageUploadResponseDto>.Failure(
                Error.Conflict(
                    ProductPreviewImageErrorCodes.MaxFilesExceeded,
                    $"A product can have at most {_settings.MaxCount} preview images."));
        }

        var existingLinks = await _files.GetProductPreviewFileLinkEntitiesAsync(productId, cancellationToken);
        var displayOrder = ResolveDisplayOrder(request.DisplayOrder, existingLinks, existingCount);

        var now = DateTime.UtcNow;
        var fileId = Guid.NewGuid();
        var fileLinkId = Guid.NewGuid();
        var originalFileName = Path.GetFileName(request.OriginalFileName.Trim());
        var generatedFileName = BuildGeneratedFileName(fileId, originalFileName);
        var objectName = BuildStorageObjectName(productId, generatedFileName);

        var uploadResult = await _storage.UploadAsync(
            new StorageUploadRequest
            {
                Content = request.Content,
                ObjectName = objectName,
                ContentType = NormalizeContentType(request.ContentType)
            },
            cancellationToken);

        var storedFile = new StoredFile
        {
            FileId = fileId,
            UploadedBy = currentUserId,
            OriginalFileName = originalFileName,
            StoredFileName = generatedFileName,
            FileUrl = uploadResult.PublicUrl,
            StoragePath = uploadResult.ObjectName,
            MimeType = NormalizeContentType(request.ContentType),
            FileExtension = NormalizeExtension(originalFileName),
            FileSizeBytes = request.FileSizeBytes,
            Status = FileStatus.ACTIVE,
            UploadedAt = now
        };

        var fileLink = new FileLink
        {
            FileLinkId = fileLinkId,
            FileId = fileId,
            ReferenceType = CatalogFileReferenceTypes.Product,
            ReferenceId = productId,
            FileType = FileType.PRODUCT_PREVIEW,
            Visibility = FileVisibility.CUSTOMER_VISIBLE,
            DisplayOrder = displayOrder,
            Description = NormalizeOptional(request.Description),
            CreatedBy = currentUserId,
            CreatedAt = now
        };

        try
        {
            await ExecuteInTransactionAsync(
                async ct =>
                {
                    if (request.DisplayOrder.HasValue)
                    {
                        ShiftDisplayOrdersForInsert(existingLinks, displayOrder);
                    }

                    await _files.AddAsync(storedFile, ct);
                    await _files.AddFileLinkAsync(fileLink, ct);
                    await _unitOfWork.SaveChangesAsync(ct);
                },
                cancellationToken);
        }
        catch
        {
            await _storage.DeleteAsync(uploadResult.ObjectName, cancellationToken);
            throw;
        }

        return ServiceResult<ProductPreviewImageUploadResponseDto>.Created(
            new ProductPreviewImageUploadResponseDto
            {
                FileId = fileId,
                Url = uploadResult.PublicUrl,
                DisplayOrder = displayOrder,
                FileType = FileType.PRODUCT_PREVIEW,
                Description = fileLink.Description,
                CreatedAt = now
            },
            "Product preview image uploaded successfully.");
    }

    public async Task<ServiceResult<ProductPreviewImageListResponseDto>> GetListAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty)
        {
            return ServiceResult<ProductPreviewImageListResponseDto>.BadRequest(ProductValidationMessages.ProductIdRequired);
        }

        if (await _products.GetByIdAsync(productId, cancellationToken) is null)
        {
            return ServiceResult<ProductPreviewImageListResponseDto>.NotFound(ProductValidationMessages.ProductNotFound);
        }

        var previews = await _files.GetProductPreviewFilesAsync(productId, cancellationToken);
        return ServiceResult<ProductPreviewImageListResponseDto>.Success(
            new ProductPreviewImageListResponseDto
            {
                ProductId = productId,
                Items = MapPreviewItems(previews)
            },
            "Product preview images retrieved successfully.");
    }

    public async Task<ServiceResult<ProductPreviewImageListResponseDto>> ReorderAsync(
        Guid productId,
        ReorderProductPreviewImagesRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty)
        {
            return ServiceResult<ProductPreviewImageListResponseDto>.BadRequest(ProductValidationMessages.ProductIdRequired);
        }

        if (await _products.GetByIdAsync(productId, cancellationToken) is null)
        {
            return ServiceResult<ProductPreviewImageListResponseDto>.NotFound(ProductValidationMessages.ProductNotFound);
        }

        var fileLinks = (await _files.GetProductPreviewFileLinkEntitiesAsync(productId, cancellationToken)).ToList();
        if (fileLinks.Count == 0)
        {
            return ServiceResult<ProductPreviewImageListResponseDto>.Success(
                new ProductPreviewImageListResponseDto { ProductId = productId, Items = [] },
                "Product preview images reordered successfully.");
        }

        var reorderError = TryBuildReorderMap(request, fileLinks, out var orderByFileId);
        if (reorderError is not null)
        {
            return ServiceResult<ProductPreviewImageListResponseDto>.Failure(reorderError);
        }

        await ExecuteInTransactionAsync(
            async ct =>
            {
                foreach (var link in fileLinks)
                {
                    link.DisplayOrder = orderByFileId![link.FileId];
                }

                await _unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);

        var previews = await _files.GetProductPreviewFilesAsync(productId, cancellationToken);
        return ServiceResult<ProductPreviewImageListResponseDto>.Success(
            new ProductPreviewImageListResponseDto
            {
                ProductId = productId,
                Items = MapPreviewItems(previews)
            },
            "Product preview images reordered successfully.");
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

        if (await _products.GetByIdAsync(productId, cancellationToken) is null)
        {
            return ServiceResult<DeleteProductPreviewImageResponseDto>.NotFound(ProductValidationMessages.ProductNotFound);
        }

        if (await _files.GetProductPreviewFileAsync(productId, fileId, cancellationToken) is null)
        {
            return ServiceResult<DeleteProductPreviewImageResponseDto>.Failure(
                Error.NotFound(
                    ProductPreviewImageErrorCodes.PreviewFileNotFound,
                    "Product preview image not found."));
        }

        var file = await _files.GetByIdAsync(fileId, cancellationToken);
        if (file is null)
        {
            return ServiceResult<DeleteProductPreviewImageResponseDto>.Failure(
                Error.NotFound(
                    ProductPreviewImageErrorCodes.PreviewFileNotFound,
                    "Product preview image not found."));
        }

        await _storage.DeleteAsync(file.StoragePath, cancellationToken);

        var fileLinks = await _files.GetFileLinkEntitiesByFileIdAsync(fileId, cancellationToken);
        var remainingLinks = (await _files.GetProductPreviewFileLinkEntitiesAsync(productId, cancellationToken))
            .Where(link => link.FileId != fileId)
            .OrderBy(link => link.DisplayOrder ?? int.MaxValue)
            .ThenBy(link => link.CreatedAt)
            .ToList();

        await ExecuteInTransactionAsync(
            async ct =>
            {
                _files.RemoveFileLinks(fileLinks);
                _files.Remove(file);
                ReindexDisplayOrders(remainingLinks);
                await _unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);

        return ServiceResult<DeleteProductPreviewImageResponseDto>.Success(
            new DeleteProductPreviewImageResponseDto
            {
                FileId = fileId,
                ProductId = productId,
                DeletedAt = DateTime.UtcNow
            },
            "Product preview image deleted successfully.");
    }

    private Error? ValidateUploadRequest(UploadProductPreviewImageRequestDto request)
    {
        if (request.Content == Stream.Null || !request.Content.CanRead)
        {
            return Error.BadRequest(ProductPreviewImageErrorCodes.InvalidFileType, "File is required.");
        }

        if (string.IsNullOrWhiteSpace(request.OriginalFileName))
        {
            return Error.BadRequest(ProductPreviewImageErrorCodes.InvalidFileType, "Original file name is required.");
        }

        if (request.FileSizeBytes <= 0)
        {
            return Error.BadRequest(ProductPreviewImageErrorCodes.InvalidFileType, "File size must be greater than zero.");
        }

        if (request.FileSizeBytes > _settings.MaxFileSizeBytes)
        {
            return Error.PayloadTooLarge(
                ProductPreviewImageErrorCodes.FileTooLarge,
                $"File size must not exceed {_settings.MaxFileSizeBytes} bytes.");
        }

        var extension = Path.GetExtension(request.OriginalFileName);
        if (string.IsNullOrWhiteSpace(extension) ||
            !AllowedExtensions().Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return Error.UnsupportedMediaType(
                ProductPreviewImageErrorCodes.InvalidFileType,
                "File extension is not allowed for product preview images.");
        }

        var contentType = NormalizeContentType(request.ContentType);
        if (!AllowedMimeTypes().Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return Error.UnsupportedMediaType(
                ProductPreviewImageErrorCodes.InvalidFileType,
                "File MIME type is not allowed for product preview images.");
        }

        if (request.DisplayOrder is <= 0 || request.DisplayOrder > _settings.MaxCount)
        {
            return Error.BadRequest(
                ProductPreviewImageErrorCodes.InvalidFileType,
                $"Display order must be between 1 and {_settings.MaxCount}.");
        }

        return null;
    }

    private static int ResolveDisplayOrder(
        int? requestedOrder,
        IReadOnlyList<FileLink> existingLinks,
        int existingCount)
    {
        if (!requestedOrder.HasValue)
        {
            if (existingCount == 0)
            {
                return 1;
            }

            var maxOrder = existingLinks.Max(link => link.DisplayOrder ?? 0);
            return maxOrder <= 0 ? existingCount + 1 : maxOrder + 1;
        }

        return requestedOrder.Value;
    }

    private static void ShiftDisplayOrdersForInsert(IReadOnlyList<FileLink> existingLinks, int insertOrder)
    {
        foreach (var link in existingLinks.Where(link => (link.DisplayOrder ?? int.MaxValue) >= insertOrder))
        {
            link.DisplayOrder = (link.DisplayOrder ?? 0) + 1;
        }
    }

    private static Error? TryBuildReorderMap(
        ReorderProductPreviewImagesRequestDto request,
        IReadOnlyList<FileLink> fileLinks,
        out Dictionary<Guid, int>? orderByFileId)
    {
        orderByFileId = null;
        var hasFileIds = request.FileIds is { Count: > 0 };
        var hasItems = request.Items is { Count: > 0 };

        if (hasFileIds == hasItems)
        {
            return Error.BadRequest(
                ProductPreviewImageErrorCodes.InvalidReorderPayload,
                "Provide either fileIds or items, but not both.");
        }

        var expectedIds = fileLinks.Select(link => link.FileId).ToHashSet();

        if (hasFileIds)
        {
            var fileIds = request.FileIds!;
            if (fileIds.Count != expectedIds.Count || fileIds.Any(id => !expectedIds.Contains(id)))
            {
                return Error.BadRequest(
                    ProductPreviewImageErrorCodes.InvalidReorderPayload,
                    "fileIds must include every preview image exactly once.");
            }

            orderByFileId = fileIds
                .Select((fileId, index) => new { fileId, Order = index + 1 })
                .ToDictionary(item => item.fileId, item => item.Order);
            return null;
        }

        var items = request.Items!;
        if (items.Count != expectedIds.Count || items.Any(item => !expectedIds.Contains(item.FileId)))
        {
            return Error.BadRequest(
                ProductPreviewImageErrorCodes.InvalidReorderPayload,
                "items must include every preview image exactly once.");
        }

        orderByFileId = items
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.FileId)
            .Select((item, index) => new { item.FileId, Order = index + 1 })
            .ToDictionary(item => item.FileId, item => item.Order);
        return null;
    }

    private static void ReindexDisplayOrders(List<FileLink> fileLinks)
    {
        for (var index = 0; index < fileLinks.Count; index++)
        {
            fileLinks[index].DisplayOrder = index + 1;
        }
    }

    private static List<ProductPreviewImageDto> MapPreviewItems(
        IReadOnlyList<Infrastructure.DTOs.Products.ProductPreviewImageReadModel> previews)
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

    private string[] AllowedExtensions()
    {
        return _settings.AllowedExtensions.Length == 0
            ? new ProductPreviewImageSettings().AllowedExtensions
            : _settings.AllowedExtensions;
    }

    private string[] AllowedMimeTypes()
    {
        return _settings.AllowedMimeTypes.Length == 0
            ? new ProductPreviewImageSettings().AllowedMimeTypes
            : _settings.AllowedMimeTypes;
    }

    private string BuildStorageObjectName(Guid productId, string generatedFileName)
    {
        var prefix = string.IsNullOrWhiteSpace(_firebaseSettings.ProductFilesPrefix)
            ? "products"
            : _firebaseSettings.ProductFilesPrefix.Trim().Trim('/');

        return $"{prefix}/{productId:D}/{generatedFileName}";
    }

    private static string BuildGeneratedFileName(Guid fileId, string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        return $"{fileId:N}{extension}";
    }

    private static string? NormalizeExtension(string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName);
        return string.IsNullOrWhiteSpace(extension) ? null : extension.TrimStart('.').ToLowerInvariant();
    }

    private static string NormalizeContentType(string? contentType)
    {
        return string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await action(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
