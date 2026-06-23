using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Interfaces.Products;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.DTOs.Products;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Infrastructure.Storage;
using Mapster;
using Microsoft.Extensions.Options;

namespace FurniSpace.Application.Services.Products;

public sealed class ProductService : IProductService
{
    private static readonly HashSet<FileType> AllowedProductFileTypes =
    [
        FileType.PRODUCT_PREVIEW,
        FileType.OTHER
    ];

    private readonly IProductRepository _products;
    private readonly IProjectFileRepository _files;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _storage;
    private readonly FileUploadSettings _uploadSettings;
    private readonly ProductPreviewImageSettings _previewSettings;
    private readonly FirebaseStorageSettings _firebaseSettings;

    public ProductService(
        IProductRepository products,
        IProjectFileRepository files,
        IFileStorageService storage,
        IOptions<FileUploadSettings> uploadSettings,
        IOptions<ProductPreviewImageSettings> previewSettings,
        IOptions<FirebaseStorageSettings> firebaseSettings,
        IUnitOfWork unitOfWork)
    {
        _products = products;
        _files = files;
        _unitOfWork = unitOfWork;
        _storage = storage;
        _uploadSettings = uploadSettings.Value;
        _previewSettings = previewSettings.Value;
        _firebaseSettings = firebaseSettings.Value;
    }

    public async Task<ServiceResult<ProductDto>> CreateAsync(
        CreateProductRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateCreateRequest(request);
        if (errors.Count > 0)
        {
            return ServiceResult<ProductDto>.BadRequest(errors);
        }

        if (await _products.GetCategoryAsync(request.CategoryId, cancellationToken) is null)
        {
            return ServiceResult<ProductDto>.BadRequest("Category does not exist.");
        }

        var productCode = NormalizeOptional(request.ProductCode);
        if (productCode is not null &&
            await _products.ProductCodeExistsAsync(productCode, cancellationToken))
        {
            return ServiceResult<ProductDto>.Conflict("Product code already exists.");
        }

        var product = new Product
        {
            ProductId = Guid.NewGuid(),
            CategoryId = request.CategoryId,
            ProductCode = productCode,
            ProductName = request.ProductName.Trim(),
            Description = CatalogFileStorageHelpers.NormalizeOptional(request.Description)
        };

        await _products.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        product.Status ??= ProductStatus.ACTIVE;

        return ServiceResult<ProductDto>.Created(product.Adapt<ProductDto>(), "Product master created successfully.");
    }

    public async Task<ServiceResult<ProductDto>> UpdateAsync(
        Guid productId,
        UpdateProductRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty)
        {
            return ServiceResult<ProductDto>.BadRequest(ProductValidationMessages.ProductIdRequired);
        }

        var errors = ValidateUpdateRequest(request);
        if (errors.Count > 0)
        {
            return ServiceResult<ProductDto>.BadRequest(errors);
        }

        var product = await _products.GetByIdAsync(productId, cancellationToken);
        if (product is null)
        {
            return ServiceResult<ProductDto>.NotFound(ProductValidationMessages.ProductNotFound);
        }

        if (await _products.GetCategoryAsync(request.CategoryId, cancellationToken) is null)
        {
            return ServiceResult<ProductDto>.BadRequest("Category does not exist.");
        }

        product.CategoryId = request.CategoryId;
        product.ProductName = request.ProductName.Trim();
        product.Description = NormalizeOptional(request.Description);
        product.Status ??= ProductStatus.ACTIVE;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProductDto>.Success(
            product.Adapt<ProductDto>(),
            "Product master updated successfully.");
    }

    public async Task<ServiceResult<ProductDetailDto>> GetByIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty)
        {
            return ServiceResult<ProductDetailDto>.BadRequest(ProductValidationMessages.ProductIdRequired);
        }

        var product = await _products.GetDetailAsync(productId, cancellationToken);
        if (product is null)
        {
            return ServiceResult<ProductDetailDto>.NotFound(ProductValidationMessages.ProductNotFound);
        }

        var detail = product.Adapt<ProductDetailDto>();
        await EnrichDetailAsync(detail, cancellationToken);
        return ServiceResult<ProductDetailDto>.Success(detail, string.Empty);
    }

    public async Task<ServiceResult<ProductListResponseDto>> GetAllAsync(
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidatePagination(page, limit);
        if (validationError is not null)
        {
            return ServiceResult<ProductListResponseDto>.BadRequest(validationError);
        }

        var products = await _products.GetPublicListAsync(page, limit, cancellationToken);
        var total = await _products.CountAsync(cancellationToken);
        var items = products.Adapt<List<ProductListItemDto>>();
        await EnrichListItemsAsync(items, cancellationToken);

        return ServiceResult<ProductListResponseDto>.Success(
            new ProductListResponseDto
            {
                Items = items,
                Page = page,
                Limit = limit,
                Total = total
            },
            string.Empty);
    }

    public async Task<ServiceResult<ProductByCategoryResponseDto>> GetByCategoryAsync(
        Guid categoryId,
        int page,
        int limit,
        bool includeDefaultVersion,
        CancellationToken cancellationToken = default)
    {
        if (categoryId == Guid.Empty)
        {
            return ServiceResult<ProductByCategoryResponseDto>.BadRequest("Category id is required.");
        }

        var validationError = ValidatePagination(page, limit);
        if (validationError is not null)
        {
            return ServiceResult<ProductByCategoryResponseDto>.BadRequest(validationError);
        }

        var category = await _products.GetCategoryAsync(categoryId, cancellationToken);
        if (category is null)
        {
            return ServiceResult<ProductByCategoryResponseDto>.NotFound("Category not found.");
        }

        var products = await _products.GetPublicListByCategoryAsync(
            categoryId,
            page,
            limit,
            includeDefaultVersion,
            cancellationToken);
        var total = await _products.CountByCategoryAsync(categoryId, cancellationToken);
        var items = products.Adapt<List<ProductListItemDto>>();
        await EnrichListItemsAsync(items, cancellationToken);

        return ServiceResult<ProductByCategoryResponseDto>.Success(
            new ProductByCategoryResponseDto
            {
                Category = category.Adapt<ProductCategorySummaryDto>(),
                Items = items,
                Page = page,
                Limit = limit,
                Total = total
            },
            string.Empty);
    }

    public async Task<ServiceResult<CatalogFileUploadResponseDto>> UploadFileAsync(
        Guid productId,
        Guid currentUserId,
        UploadCatalogFileRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.BadRequest(ProductValidationMessages.ProductIdRequired);
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.Unauthorized("Authenticated account id is required.");
        }

        if (request.FileType == FileType.PRODUCT_PREVIEW)
        {
            return await UploadPreviewFileAsync(productId, currentUserId, request, cancellationToken);
        }

        var validationErrors = CatalogFileUploadValidation.ValidateGeneralUpload(
            request,
            _uploadSettings,
            _firebaseSettings,
            AllowedProductFileTypes);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.BadRequest(validationErrors);
        }

        if (await _products.GetByIdAsync(productId, cancellationToken) is null)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.Failure(
                Error.NotFound(
                    ProductPreviewImageErrorCodes.ProductNotFound,
                    ProductValidationMessages.ProductNotFound));
        }

        return await PersistUploadedFileAsync(
            productId,
            currentUserId,
            request,
            cancellationToken);
    }

    private async Task<ServiceResult<CatalogFileUploadResponseDto>> UploadPreviewFileAsync(
        Guid productId,
        Guid currentUserId,
        UploadCatalogFileRequestDto request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidatePreviewUploadRequest(request);
        if (validationError is not null)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.Failure(validationError);
        }

        if (await _products.GetByIdAsync(productId, cancellationToken) is null)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.Failure(
                Error.NotFound(
                    ProductPreviewImageErrorCodes.ProductNotFound,
                    ProductValidationMessages.ProductNotFound));
        }

        var existingCount = await _files.CountProductPreviewFilesAsync(productId, cancellationToken);
        if (existingCount >= _previewSettings.MaxCount)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.Failure(
                Error.Conflict(
                    ProductPreviewImageErrorCodes.MaxFilesExceeded,
                    $"A product can have at most {_previewSettings.MaxCount} preview images."));
        }

        var existingLinks = (await _files.GetProductPreviewFileLinkEntitiesAsync(productId, cancellationToken))
            .ToList();
        var displayOrder = PreviewImageFileLinkOrdering.ResolveInsertDisplayOrder(
            request.DisplayOrder,
            existingLinks,
            existingCount);

        var now = DateTime.UtcNow;
        var fileId = Guid.NewGuid();
        var fileLinkId = Guid.NewGuid();
        var originalFileName = CatalogFileStorageHelpers.NormalizeOriginalFileName(request.OriginalFileName);
        var generatedFileName = CatalogFileStorageHelpers.BuildGeneratedFileName(fileId, originalFileName);
        var objectName = CatalogFileStorageHelpers.BuildStorageObjectName(
            "products",
            _firebaseSettings.ProductFilesPrefix,
            productId,
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
                        ReferenceType = CatalogFileReferenceTypes.Product,
                        ReferenceId = productId,
                        FileType = FileType.PRODUCT_PREVIEW,
                        Visibility = visibility,
                        CreatedBy = currentUserId,
                        CreatedAt = now,
                        Description = request.Description,
                        DisplayOrder = displayOrder
                    });

                    await _files.AddAsync(storedFile, ct);
                    await _files.AddFileLinkAsync(fileLink, ct);

                    var allPreviewLinks = (await _files.GetProductPreviewFileLinkEntitiesAsync(productId, ct))
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
                            ReferenceType = CatalogFileReferenceTypes.Product,
                            ReferenceId = productId,
                            OriginalFileName = originalFileName,
                            Request = request,
                            UploadResult = uploadResult,
                            StoredFile = storedFile,
                            FileLink = uploadedLink,
                            Visibility = visibility,
                            CurrentUserId = currentUserId,
                            UploadedAt = now
                        }),
                        "Product file uploaded successfully.");
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
        Guid productId,
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
            "products",
            _firebaseSettings.ProductFilesPrefix,
            productId,
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
            ReferenceType = CatalogFileReferenceTypes.Product,
            ReferenceId = productId,
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
                ReferenceType = CatalogFileReferenceTypes.Product,
                ReferenceId = productId,
                OriginalFileName = originalFileName,
                Request = request,
                UploadResult = uploadResult,
                StoredFile = storedFile,
                FileLink = fileLink,
                Visibility = visibility,
                CurrentUserId = currentUserId,
                UploadedAt = now
            }),
            "Product file uploaded successfully.");
    }

    private Error? ValidatePreviewUploadRequest(UploadCatalogFileRequestDto request)
    {
        var validationError = CatalogPreviewUploadValidation.ValidateFileContent(
            request,
            _previewSettings,
            ProductPreviewImageErrorCodes.InvalidFileType,
            ProductPreviewImageErrorCodes.FileTooLarge);
        if (validationError is not null)
        {
            return validationError;
        }

        return CatalogPreviewUploadValidation.ValidateDisplayOrderGreaterThanZero(
            request.DisplayOrder,
            ProductPreviewImageErrorCodes.InvalidDisplayOrder);
    }

    private async Task EnrichListItemsAsync(
        List<ProductListItemDto> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        const bool customerVisibleOnly = true;
        var productIds = items.Select(item => item.ProductId).ToList();
        var versionIds = items
            .Where(item => item.DefaultVersion is not null)
            .Select(item => item.DefaultVersion!.ProductVersionId)
            .ToList();

        var productFiles = await _files.GetCatalogFilesByReferencesAsync(
            CatalogFileReferenceTypes.Product,
            productIds,
            customerVisibleOnly,
            cancellationToken);
        var versionFiles = versionIds.Count == 0
            ? []
            : await _files.GetCatalogFilesByReferencesAsync(
                CatalogFileReferenceTypes.ProductVersion,
                versionIds,
                customerVisibleOnly,
                cancellationToken);

        var productFilesById = GroupByReferenceId(productFiles);
        var versionFilesById = GroupByReferenceId(versionFiles);

        foreach (var item in items)
        {
            if (productFilesById.TryGetValue(item.ProductId, out var files))
            {
                item.Thumbnail = PickThumbnail(files, customerVisibleOnly);
            }

            if (item.DefaultVersion is not null &&
                versionFilesById.TryGetValue(item.DefaultVersion.ProductVersionId, out var versionFileList))
            {
                item.DefaultVersion.Thumbnail = PickThumbnail(versionFileList, customerVisibleOnly);
            }
        }
    }

    private async Task EnrichDetailAsync(ProductDetailDto detail, CancellationToken cancellationToken)
    {
        const bool customerVisibleOnly = true;
        var productFiles = await _files.GetCatalogFilesByReferencesAsync(
            CatalogFileReferenceTypes.Product,
            [detail.ProductId],
            customerVisibleOnly,
            cancellationToken);
        detail.Files = ToCatalogFileList(productFiles, customerVisibleOnly);
        detail.Thumbnail = PickThumbnail(productFiles, customerVisibleOnly);

        var versionIds = detail.Versions.Select(version => version.ProductVersionId).ToList();
        if (versionIds.Count == 0)
        {
            return;
        }

        var versionFiles = await _files.GetCatalogFilesByReferencesAsync(
            CatalogFileReferenceTypes.ProductVersion,
            versionIds,
            customerVisibleOnly,
            cancellationToken);
        var versionFilesById = GroupByReferenceId(versionFiles);

        foreach (var version in detail.Versions)
        {
            if (!versionFilesById.TryGetValue(version.ProductVersionId, out var files))
            {
                continue;
            }

            version.Files = ToCatalogFileList(files, customerVisibleOnly);
            version.Thumbnail = PickThumbnail(files, customerVisibleOnly);
        }

        if (detail.DefaultVersion is not null &&
            versionFilesById.TryGetValue(detail.DefaultVersion.ProductVersionId, out var defaultFiles))
        {
            detail.DefaultVersion.Files = ToCatalogFileList(defaultFiles, customerVisibleOnly);
            detail.DefaultVersion.Thumbnail = PickThumbnail(defaultFiles, customerVisibleOnly);
        }
    }

    private static string? ValidatePagination(int page, int limit)
    {
        if (page < 1)
        {
            return "Page must be greater than zero.";
        }

        if (limit is < 1 or > 100)
        {
            return "Limit must be between 1 and 100.";
        }

        return null;
    }

    private static List<string> ValidateCreateRequest(CreateProductRequestDto request)
    {
        var errors = new List<string>();
        if (request.CategoryId == Guid.Empty)
        {
            errors.Add("Category id is required.");
        }

        if (request.ProductCode?.Trim().Length > 50)
        {
            errors.Add("Product code must not exceed 50 characters.");
        }

        if (string.IsNullOrWhiteSpace(request.ProductName))
        {
            errors.Add("Product name is required.");
        }
        else if (request.ProductName.Trim().Length > 150)
        {
            errors.Add("Product name must not exceed 150 characters.");
        }

        return errors;
    }

    private static List<string> ValidateUpdateRequest(UpdateProductRequestDto request)
    {
        var errors = new List<string>();
        if (request.CategoryId == Guid.Empty)
        {
            errors.Add("Category id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ProductName))
        {
            errors.Add("Product name is required.");
        }
        else if (request.ProductName.Trim().Length > 150)
        {
            errors.Add("Product name must not exceed 150 characters.");
        }

        return errors;
    }

    private static string? NormalizeOptional(string? value)
        => CatalogFileStorageHelpers.NormalizeOptional(value);

    private static CatalogFileDto? PickThumbnail(
        IEnumerable<CatalogFileReadModel> files,
        bool customerVisibleOnly)
    {
        var visibleFiles = CatalogFileOrdering.FilterVisible(files, customerVisibleOnly).ToList();
        if (visibleFiles.Count == 0)
        {
            return null;
        }

        var preview = CatalogFileOrdering.PickPreviewThumbnail(visibleFiles);
        return (preview ?? visibleFiles[0]).Adapt<CatalogFileDto>();
    }

    private static List<CatalogFileDto> ToCatalogFileList(
        IEnumerable<CatalogFileReadModel> files,
        bool customerVisibleOnly)
    {
        return CatalogFileOrdering
            .SortCatalogFiles(CatalogFileOrdering.FilterVisible(files, customerVisibleOnly))
            .Adapt<List<CatalogFileDto>>();
    }

    private static Dictionary<Guid, List<CatalogFileReadModel>> GroupByReferenceId(
        IEnumerable<CatalogFileReadModel> files)
    {
        return files
            .GroupBy(file => file.ReferenceId)
            .ToDictionary(group => group.Key, group => group.ToList());
    }
}
