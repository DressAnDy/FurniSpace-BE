using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProductVersions;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Interfaces.ProductVersions;
using FurniSpace.Application.Interfaces.Search;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;
using Microsoft.Extensions.Options;

namespace FurniSpace.Application.Services.ProductVersions;

public sealed class ProductVersionService : IProductVersionService
{
    private const string ProductVersionIdRequiredMessage = "Product version id is required.";
    private const string ProductVersionNotFoundMessage = "Product version not found.";
    private const string PreviewFileNotFoundMessage = "Product version preview image not found.";

    private static readonly HashSet<FileType> AllowedProductVersionFileTypes =
    [
        FileType.PRODUCT_PREVIEW,
        FileType.MODEL_3D,
        FileType.TEXTURE,
        FileType.OTHER
    ];

    private readonly IProductVersionRepository _productVersions;
    private readonly IProjectFileRepository _files;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _storage;
    private readonly IProductSearchIndexer _productSearchIndexer;
    private readonly FileUploadSettings _uploadSettings;
    private readonly ProductPreviewImageSettings _previewSettings;
    private readonly FirebaseStorageSettings _firebaseSettings;

    public ProductVersionService(
        IProductVersionRepository productVersions,
        IProjectFileRepository files,
        IFileStorageService storage,
        IOptions<FileUploadSettings> uploadSettings,
        IOptions<ProductPreviewImageSettings> previewSettings,
        IOptions<FirebaseStorageSettings> firebaseSettings,
        IProductSearchIndexer productSearchIndexer,
        IUnitOfWork unitOfWork)
    {
        _productVersions = productVersions;
        _files = files;
        _unitOfWork = unitOfWork;
        _storage = storage;
        _productSearchIndexer = productSearchIndexer;
        _uploadSettings = uploadSettings.Value;
        _previewSettings = previewSettings.Value;
        _firebaseSettings = firebaseSettings.Value;
    }

    public async Task<ServiceResult<ProductVersionDto>> CreateAsync(
        Guid productId,
        CreateProductVersionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty)
        {
            return ServiceResult<ProductVersionDto>.BadRequest("Product id is required.");
        }

        var errors = ValidateCreateRequest(request);
        if (errors.Count > 0)
        {
            return ServiceResult<ProductVersionDto>.BadRequest(errors);
        }

        if (!await _productVersions.ProductExistsAsync(productId, cancellationToken))
        {
            return ServiceResult<ProductVersionDto>.NotFound("Product not found.");
        }

        var versionCode = request.VersionCode.Trim();
        if (await _productVersions.VersionCodeExistsAsync(versionCode, cancellationToken))
        {
            return ServiceResult<ProductVersionDto>.Conflict("Product version code already exists.");
        }

        var productVersion = new ProductVersion
        {
            ProductVersionId = Guid.NewGuid(),
            ProductId = productId,
            VersionCode = versionCode,
            VersionName = request.VersionName.Trim(),
            VersionType = request.VersionType ?? ProductVersionType.STANDARD,
            Material = CatalogFileStorageHelpers.NormalizeOptional(request.Material),
            Color = CatalogFileStorageHelpers.NormalizeOptional(request.Color),
            Width = request.Width,
            Height = request.Height,
            Depth = request.Depth,
            EstimatedPrice = request.EstimatedPrice,
            IsDefault = request.IsDefault ?? false,
            IsPublic = request.IsPublic ?? true,
            IsProjectSpecific = request.IsProjectSpecific ?? false
        };

        if (productVersion.IsDefault == true)
        {
            await _productVersions.SetDefaultAsync(productVersion, cancellationToken);
        }

        await _productVersions.AddAsync(productVersion, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        productVersion.Status ??= ProductStatus.ACTIVE;
        await _productSearchIndexer.SyncProductAsync(productId, cancellationToken);

        return ServiceResult<ProductVersionDto>.Created(
            ToVersionDto(productVersion),
            "Product version created successfully.");
    }

    public async Task<ServiceResult<ProductVersionDto>> UpdateAsync(
        Guid productVersionId,
        UpdateProductVersionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (productVersionId == Guid.Empty)
        {
            return ServiceResult<ProductVersionDto>.BadRequest(ProductVersionIdRequiredMessage);
        }

        var errors = ValidateUpdateRequest(request);
        if (errors.Count > 0)
        {
            return ServiceResult<ProductVersionDto>.BadRequest(errors);
        }

        var productVersion = await _productVersions.GetByIdAsync(productVersionId, cancellationToken);
        if (productVersion is null)
        {
            return ServiceResult<ProductVersionDto>.NotFound(ProductVersionNotFoundMessage);
        }

        productVersion.VersionName = request.VersionName.Trim();
        productVersion.VersionType = request.VersionType ?? ProductVersionType.STANDARD;
        productVersion.Material = NormalizeOptional(request.Material);
        productVersion.Color = NormalizeOptional(request.Color);
        productVersion.Width = request.Width;
        productVersion.Height = request.Height;
        productVersion.Depth = request.Depth;
        productVersion.EstimatedPrice = request.EstimatedPrice;
        productVersion.IsPublic = request.IsPublic ?? true;
        productVersion.IsProjectSpecific = request.IsProjectSpecific ?? false;
        productVersion.Status ??= ProductStatus.ACTIVE;

        if (request.IsDefault == true)
        {
            await _productVersions.SetDefaultAsync(productVersion, cancellationToken);
        }
        else
        {
            productVersion.IsDefault = request.IsDefault ?? false;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _productSearchIndexer.SyncProductAsync(productVersion.ProductId, cancellationToken);

        return ServiceResult<ProductVersionDto>.Success(
            ToVersionDto(productVersion),
            "Product version updated successfully.");
    }

    public async Task<ServiceResult<SetDefaultProductVersionDto>> SetDefaultAsync(
        Guid productVersionId,
        CancellationToken cancellationToken = default)
    {
        if (productVersionId == Guid.Empty)
        {
            return ServiceResult<SetDefaultProductVersionDto>.BadRequest(ProductVersionIdRequiredMessage);
        }

        var productVersion = await _productVersions.GetByIdAsync(productVersionId, cancellationToken);
        if (productVersion is null)
        {
            return ServiceResult<SetDefaultProductVersionDto>.NotFound(ProductVersionNotFoundMessage);
        }

        await _productVersions.SetDefaultAsync(productVersion, cancellationToken);
        productVersion.IsDefault = true;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _productSearchIndexer.SyncProductAsync(productVersion.ProductId, cancellationToken);

        return ServiceResult<SetDefaultProductVersionDto>.Success(
            new SetDefaultProductVersionDto
            {
                ProductVersionId = productVersion.ProductVersionId,
                ProductId = productVersion.ProductId,
                IsDefault = productVersion.IsDefault == true
            },
            "Default product version updated successfully.");
    }

    public async Task<ServiceResult<ProductVersionDetailDto>> GetByIdAsync(
        Guid productVersionId,
        CancellationToken cancellationToken = default)
    {
        if (productVersionId == Guid.Empty)
        {
            return ServiceResult<ProductVersionDetailDto>.BadRequest(ProductVersionIdRequiredMessage);
        }

        var version = await _productVersions.GetPublicDetailAsync(productVersionId, cancellationToken);
        if (version is null)
        {
            return ServiceResult<ProductVersionDetailDto>.NotFound(ProductVersionNotFoundMessage);
        }

        var detail = version.Adapt<ProductVersionDetailDto>();
        const bool customerVisibleOnly = true;
        var versionFiles = await _files.GetCatalogFilesByReferencesAsync(
            CatalogFileReferenceTypes.ProductVersion,
            [productVersionId],
            customerVisibleOnly,
            cancellationToken);
        detail.Files = ToCatalogFileList(versionFiles, customerVisibleOnly);

        return ServiceResult<ProductVersionDetailDto>.Success(detail, string.Empty);
    }

    public async Task<ServiceResult<CatalogFileUploadResponseDto>> UploadFileAsync(
        Guid productVersionId,
        Guid currentUserId,
        UploadCatalogFileRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (productVersionId == Guid.Empty)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.BadRequest(ProductVersionIdRequiredMessage);
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.Unauthorized("Authenticated account id is required.");
        }

        if (request.FileType == FileType.PRODUCT_PREVIEW)
        {
            return await UploadPreviewFileAsync(productVersionId, currentUserId, request, cancellationToken);
        }

        var validationErrors = CatalogFileUploadValidation.ValidateGeneralUpload(
            request,
            _uploadSettings,
            _firebaseSettings,
            AllowedProductVersionFileTypes);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.BadRequest(validationErrors);
        }

        if (await _productVersions.GetByIdAsync(productVersionId, cancellationToken) is null)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.Failure(
                Error.NotFound(
                    ProductVersionPreviewErrorCodes.ProductVersionNotFound,
                    ProductVersionNotFoundMessage));
        }

        return await PersistUploadedFileAsync(
            productVersionId,
            currentUserId,
            request,
            cancellationToken);
    }

    public async Task<ServiceResult<IReadOnlyList<ProductVersionPreviewReorderItemDto>>> ReorderPreviewFilesAsync(
        Guid productVersionId,
        ReorderProductVersionPreviewFilesRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (productVersionId == Guid.Empty)
        {
            return ServiceResult<IReadOnlyList<ProductVersionPreviewReorderItemDto>>.BadRequest(
                ProductVersionIdRequiredMessage);
        }

        if (await _productVersions.GetByIdAsync(productVersionId, cancellationToken) is null)
        {
            return ServiceResult<IReadOnlyList<ProductVersionPreviewReorderItemDto>>.Failure(
                Error.NotFound(
                    ProductVersionPreviewErrorCodes.ProductVersionNotFound,
                    ProductVersionNotFoundMessage));
        }

        var fileLinks = (await _files.GetProductVersionPreviewFileLinkEntitiesAsync(productVersionId, cancellationToken))
            .ToList();
        var expectedIds = fileLinks.Select(link => link.FileId).ToHashSet();
        var fileIds = request.FileIds ?? [];

        if (fileLinks.Count == 0 && fileIds.Count == 0)
        {
            return ServiceResult<IReadOnlyList<ProductVersionPreviewReorderItemDto>>.Success(
                Array.Empty<ProductVersionPreviewReorderItemDto>(),
                "Product version preview images reordered successfully.");
        }

        var foreignFileError = await ValidateForeignPreviewFileIdsAsync(
            productVersionId,
            fileIds,
            expectedIds,
            cancellationToken);
        if (foreignFileError is not null)
        {
            return ServiceResult<IReadOnlyList<ProductVersionPreviewReorderItemDto>>.Failure(foreignFileError);
        }

        if (!PreviewImageFileLinkOrdering.TryBuildExactReorderMap(
                fileIds,
                expectedIds,
                out _,
                out var validationMessage))
        {
            return ServiceResult<IReadOnlyList<ProductVersionPreviewReorderItemDto>>.Failure(
                Error.BadRequest(
                    ProductVersionPreviewErrorCodes.InvalidReorderPayload,
                    validationMessage!));
        }

        await UnitOfWorkTransactions.ExecuteAsync(
            _unitOfWork,
            async ct =>
            {
                PreviewImageFileLinkOrdering.ApplyReorderFromFileIds(fileIds, fileLinks);
                PreviewImageFileLinkOrdering.EnsureUniquePositiveDisplayOrders(fileLinks);
                await _unitOfWork.SaveChangesAsync(ct);
                return true;
            },
            cancellationToken);

        return ServiceResult<IReadOnlyList<ProductVersionPreviewReorderItemDto>>.Success(
            PreviewImageFileLinkOrdering.MapProductVersionReorderItems(fileLinks),
            "Product version preview images reordered successfully.");
    }

    public async Task<ServiceResult<DeleteProductVersionPreviewImageResponseDto>> DeletePreviewFileAsync(
        Guid productVersionId,
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        if (productVersionId == Guid.Empty)
        {
            return ServiceResult<DeleteProductVersionPreviewImageResponseDto>.BadRequest(
                ProductVersionIdRequiredMessage);
        }

        if (fileId == Guid.Empty)
        {
            return ServiceResult<DeleteProductVersionPreviewImageResponseDto>.BadRequest("File id is required.");
        }

        if (await _productVersions.GetByIdAsync(productVersionId, cancellationToken) is null)
        {
            return ServiceResult<DeleteProductVersionPreviewImageResponseDto>.Failure(
                Error.NotFound(
                    ProductVersionPreviewErrorCodes.ProductVersionNotFound,
                    ProductVersionNotFoundMessage));
        }

        var deleteValidationError = await ValidateProductVersionPreviewDeleteAsync(
            productVersionId,
            fileId,
            cancellationToken);
        if (deleteValidationError is not null)
        {
            return ServiceResult<DeleteProductVersionPreviewImageResponseDto>.Failure(deleteValidationError);
        }

        var file = await _files.GetByIdAsync(fileId, cancellationToken);
        if (file is null)
        {
            return ServiceResult<DeleteProductVersionPreviewImageResponseDto>.Failure(
                Error.NotFound(
                    ProductVersionPreviewErrorCodes.FileNotFound,
                    PreviewFileNotFoundMessage));
        }

        var fileLinks = await _files.GetFileLinkEntitiesByFileIdAsync(fileId, cancellationToken);
        var remainingLinks = (await _files.GetProductVersionPreviewFileLinkEntitiesAsync(productVersionId, cancellationToken))
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
                return true;
            },
            cancellationToken);

        await _storage.DeleteAsync(storagePath, cancellationToken);

        return ServiceResult<DeleteProductVersionPreviewImageResponseDto>.Success(
            new DeleteProductVersionPreviewImageResponseDto
            {
                DeletedFileId = fileId,
                RemainingCount = remainingCount,
                Reindexed = reindexed
            },
            "Product version preview image deleted successfully.");
    }

    private async Task<Error?> ValidateProductVersionPreviewDeleteAsync(
        Guid productVersionId,
        Guid fileId,
        CancellationToken cancellationToken)
    {
        var previewLinks = await _files.GetProductVersionPreviewFileLinkEntitiesAsync(productVersionId, cancellationToken);
        if (previewLinks.Any(link => link.FileId == fileId))
        {
            return null;
        }

        var fileLinks = await _files.GetFileLinkEntitiesByFileIdAsync(fileId, cancellationToken);
        if (fileLinks.Any(link =>
                link.FileType == FileType.PRODUCT_PREVIEW &&
                link.ReferenceType == CatalogFileReferenceTypes.ProductVersion &&
                link.ReferenceId != productVersionId))
        {
            return Error.BadRequest(
                ProductVersionPreviewErrorCodes.FileNotBelongToProductVersion,
                "The file does not belong to the specified product version.");
        }

        if (fileLinks.Any(link =>
                link.ReferenceType == CatalogFileReferenceTypes.ProductVersion &&
                link.ReferenceId == productVersionId &&
                link.FileType != FileType.PRODUCT_PREVIEW))
        {
            return Error.BadRequest(
                ProductVersionPreviewErrorCodes.InvalidFileType,
                "Only product version preview images can be deleted via this endpoint.");
        }

        if (await _files.GetByIdAsync(fileId, cancellationToken) is null)
        {
            return Error.NotFound(
                ProductVersionPreviewErrorCodes.FileNotFound,
                PreviewFileNotFoundMessage);
        }

        return Error.NotFound(
            ProductVersionPreviewErrorCodes.FileNotFound,
            PreviewFileNotFoundMessage);
    }

    private async Task<Error?> ValidateForeignPreviewFileIdsAsync(
        Guid productVersionId,
        IReadOnlyList<Guid> fileIds,
        HashSet<Guid> expectedIds,
        CancellationToken cancellationToken)
    {
        foreach (var fileId in fileIds.Where(id => !expectedIds.Contains(id)))
        {
            var links = await _files.GetFileLinkEntitiesByFileIdAsync(fileId, cancellationToken);
            var belongsToOtherVersion = links.Any(link =>
                link.FileType == FileType.PRODUCT_PREVIEW &&
                link.ReferenceType == CatalogFileReferenceTypes.ProductVersion &&
                link.ReferenceId != productVersionId);

            if (belongsToOtherVersion)
            {
                return Error.BadRequest(
                    ProductVersionPreviewErrorCodes.FileNotBelongToProductVersion,
                    "One or more file IDs do not belong to the specified product version.");
            }
        }

        return null;
    }

    private async Task<ServiceResult<CatalogFileUploadResponseDto>> UploadPreviewFileAsync(
        Guid productVersionId,
        Guid currentUserId,
        UploadCatalogFileRequestDto request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidatePreviewUploadRequest(request);
        if (validationError is not null)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.Failure(validationError);
        }

        if (await _productVersions.GetByIdAsync(productVersionId, cancellationToken) is null)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.Failure(
                Error.NotFound(
                    ProductVersionPreviewErrorCodes.ProductVersionNotFound,
                    ProductVersionNotFoundMessage));
        }

        var existingCount = await _files.CountProductVersionPreviewFilesAsync(productVersionId, cancellationToken);
        if (existingCount >= _previewSettings.MaxCount)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.Failure(
                Error.Conflict(
                    ProductVersionPreviewErrorCodes.MaxFilesExceeded,
                    $"A product version can have at most {_previewSettings.MaxCount} preview images."));
        }

        var existingLinks = (await _files.GetProductVersionPreviewFileLinkEntitiesAsync(productVersionId, cancellationToken))
            .ToList();
        var displayOrder = PreviewImageFileLinkOrdering.ResolveDisplayOrder(
            request.DisplayOrder,
            existingLinks,
            existingCount);

        var now = DateTime.UtcNow;
        var fileId = Guid.NewGuid();
        var fileLinkId = Guid.NewGuid();
        var originalFileName = CatalogFileStorageHelpers.NormalizeOriginalFileName(request.OriginalFileName);
        var generatedFileName = CatalogFileStorageHelpers.BuildGeneratedFileName(fileId, originalFileName);
        var objectName = CatalogFileStorageHelpers.BuildStorageObjectName(
            "product-versions",
            _firebaseSettings.ProductVersionFilesPrefix,
            productVersionId,
            generatedFileName);
        var visibility = request.Visibility ?? FileVisibility.CUSTOMER_VISIBLE;

        var uploadResult = await _storage.UploadAsync(
            new StorageUploadRequest
            {
                Content = request.Content,
                ObjectName = objectName,
                ContentType = CatalogFileStorageHelpers.NormalizeContentType(request.ContentType)
            },
            cancellationToken);

        try
        {
            return await UnitOfWorkTransactions.ExecuteAsync(
                _unitOfWork,
                async ct =>
                {
                    if (request.DisplayOrder.HasValue)
                    {
                        PreviewImageFileLinkOrdering.ShiftDisplayOrdersForInsert(existingLinks, displayOrder);
                    }

                    var storedFile = CatalogFileEntityFactory.CreateStoredFile(
                        fileId,
                        currentUserId,
                        originalFileName,
                        generatedFileName,
                        uploadResult,
                        request,
                        now);

                    var fileLink = CatalogFileEntityFactory.CreateFileLink(new CatalogFileLinkCreationContext
                    {
                        FileLinkId = fileLinkId,
                        FileId = fileId,
                        ReferenceType = CatalogFileReferenceTypes.ProductVersion,
                        ReferenceId = productVersionId,
                        FileType = FileType.PRODUCT_PREVIEW,
                        Visibility = visibility,
                        CreatedBy = currentUserId,
                        CreatedAt = now,
                        Description = request.Description,
                        DisplayOrder = displayOrder
                    });

                    await _files.AddAsync(storedFile, ct);
                    await _files.AddFileLinkAsync(fileLink, ct);

                    var allPreviewLinks = (await _files.GetProductVersionPreviewFileLinkEntitiesAsync(productVersionId, ct))
                        .ToList();
                    PreviewImageFileLinkOrdering.NormalizeDisplayOrdersAndPrimary(allPreviewLinks);
                    PreviewImageFileLinkOrdering.EnsureUniquePositiveDisplayOrders(allPreviewLinks);
                    await _unitOfWork.SaveChangesAsync(ct);

                    var uploadedLink = allPreviewLinks.Single(link => link.FileId == fileId);
                    return ServiceResult<CatalogFileUploadResponseDto>.Created(
                        CatalogFileUploadResponseMapper.FromUpload(new CatalogFileUploadResponseContext
                        {
                            FileId = fileId,
                            FileLinkId = fileLinkId,
                            ReferenceType = CatalogFileReferenceTypes.ProductVersion,
                            ReferenceId = productVersionId,
                            OriginalFileName = originalFileName,
                            Request = request,
                            UploadResult = uploadResult,
                            StoredFile = storedFile,
                            FileLink = uploadedLink,
                            Visibility = visibility,
                            CurrentUserId = currentUserId,
                            UploadedAt = now
                        }),
                        "Product version file uploaded successfully.");
                },
                cancellationToken);
        }
        catch
        {
            await _storage.DeleteAsync(uploadResult.ObjectName, cancellationToken);
            throw;
        }
    }

    private async Task<ServiceResult<CatalogFileUploadResponseDto>> PersistUploadedFileAsync(
        Guid productVersionId,
        Guid currentUserId,
        UploadCatalogFileRequestDto request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var fileId = Guid.NewGuid();
        var fileLinkId = Guid.NewGuid();
        var originalFileName = CatalogFileStorageHelpers.NormalizeOriginalFileName(request.OriginalFileName);
        var generatedFileName = CatalogFileStorageHelpers.BuildGeneratedFileName(fileId, originalFileName);
        var objectName = CatalogFileStorageHelpers.BuildStorageObjectName(
            "product-versions",
            _firebaseSettings.ProductVersionFilesPrefix,
            productVersionId,
            generatedFileName);
        var visibility = request.Visibility ?? FileVisibility.CUSTOMER_VISIBLE;

        var uploadResult = await _storage.UploadAsync(
            new StorageUploadRequest
            {
                Content = request.Content,
                ObjectName = objectName,
                ContentType = CatalogFileStorageHelpers.NormalizeContentType(request.ContentType)
            },
            cancellationToken);

        var storedFile = CatalogFileEntityFactory.CreateStoredFile(
            fileId,
            currentUserId,
            originalFileName,
            generatedFileName,
            uploadResult,
            request,
            now);

        var fileLink = CatalogFileEntityFactory.CreateFileLink(new CatalogFileLinkCreationContext
        {
            FileLinkId = fileLinkId,
            FileId = fileId,
            ReferenceType = CatalogFileReferenceTypes.ProductVersion,
            ReferenceId = productVersionId,
            FileType = request.FileType,
            Visibility = visibility,
            CreatedBy = currentUserId,
            CreatedAt = now,
            Description = request.Description
        });

        await _files.AddAsync(storedFile, cancellationToken);
        await _files.AddFileLinkAsync(fileLink, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<CatalogFileUploadResponseDto>.Created(
            CatalogFileUploadResponseMapper.FromUpload(new CatalogFileUploadResponseContext
            {
                FileId = fileId,
                FileLinkId = fileLinkId,
                ReferenceType = CatalogFileReferenceTypes.ProductVersion,
                ReferenceId = productVersionId,
                OriginalFileName = originalFileName,
                Request = request,
                UploadResult = uploadResult,
                StoredFile = storedFile,
                FileLink = fileLink,
                Visibility = visibility,
                CurrentUserId = currentUserId,
                UploadedAt = now
            }),
            "Product version file uploaded successfully.");
    }

    private Error? ValidatePreviewUploadRequest(UploadCatalogFileRequestDto request)
    {
        var validationError = CatalogPreviewUploadValidation.ValidateFileContent(
            request,
            _previewSettings,
            ProductVersionPreviewErrorCodes.InvalidFileType,
            ProductVersionPreviewErrorCodes.FileTooLarge);
        if (validationError is not null)
        {
            return validationError;
        }

        return CatalogPreviewUploadValidation.ValidateDisplayOrderInRange(
            request.DisplayOrder,
            _previewSettings.MaxCount,
            ProductVersionPreviewErrorCodes.InvalidDisplayOrder);
    }

    private static ProductVersionDto ToVersionDto(ProductVersion productVersion)
    {
        var dto = productVersion.Adapt<ProductVersionDto>();
        dto.Thumbnail = null;
        dto.Files = [];
        return dto;
    }

    private static List<string> ValidateCreateRequest(CreateProductVersionRequestDto request)
    {
        var errors = ValidateCommonRequest(request.VersionName);

        if (string.IsNullOrWhiteSpace(request.VersionCode))
        {
            errors.Add("Version code is required.");
        }
        else if (request.VersionCode.Trim().Length > 50)
        {
            errors.Add("Version code must not exceed 50 characters.");
        }

        return errors;
    }

    private static List<string> ValidateUpdateRequest(UpdateProductVersionRequestDto request)
    {
        return ValidateCommonRequest(request.VersionName);
    }

    private static List<string> ValidateCommonRequest(string versionName)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(versionName))
        {
            errors.Add("Version name is required.");
        }
        else if (versionName.Trim().Length > 150)
        {
            errors.Add("Version name must not exceed 150 characters.");
        }

        return errors;
    }

    private static string? NormalizeOptional(string? value)
        => CatalogFileStorageHelpers.NormalizeOptional(value);

    private static List<CatalogFileDto> ToCatalogFileList(
        IEnumerable<CatalogFileReadModel> files,
        bool customerVisibleOnly)
    {
        return CatalogFileOrdering
            .SortCatalogFiles(CatalogFileOrdering.FilterVisible(files, customerVisibleOnly))
            .Adapt<List<CatalogFileDto>>();
    }
}
