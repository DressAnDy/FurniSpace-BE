using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProductVersions;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Interfaces.ProductVersions;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.DTOs.Products;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Infrastructure.Storage;
using Mapster;
using Microsoft.Extensions.Options;

namespace FurniSpace.Application.Services.ProductVersions;

public sealed class ProductVersionService : IProductVersionService
{
    private const string ProductVersionIdRequiredMessage = "Product version id is required.";
    private const string ProductVersionNotFoundMessage = "Product version not found.";

    private static readonly HashSet<FileType> AllowedProductVersionFileTypes =
    [
        FileType.PRODUCT_PREVIEW,
        FileType.MODEL_3D,
        FileType.TEXTURE,
        FileType.OTHER
    ];

    private readonly IProductVersionRepository _productVersions;
    private readonly IProjectFileRepository _files;
    private readonly IFileStorageService _storage;
    private readonly FileUploadSettings _uploadSettings;
    private readonly FirebaseStorageSettings _firebaseSettings;

    public ProductVersionService(
        IProductVersionRepository productVersions,
        IProjectFileRepository files,
        IFileStorageService storage,
        IOptions<FileUploadSettings> uploadSettings,
        IOptions<FirebaseStorageSettings> firebaseSettings)
    {
        _productVersions = productVersions;
        _files = files;
        _storage = storage;
        _uploadSettings = uploadSettings.Value;
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
            Material = NormalizeOptional(request.Material),
            Color = NormalizeOptional(request.Color),
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
        await _productVersions.SaveChangesAsync(cancellationToken);
        productVersion.Status ??= ProductStatus.ACTIVE;

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

        await _productVersions.SaveChangesAsync(cancellationToken);

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
        await _productVersions.SaveChangesAsync(cancellationToken);

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

        var validationErrors = ValidateCatalogFileRequest(request, AllowedProductVersionFileTypes);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.BadRequest(validationErrors);
        }

        if (await _productVersions.GetByIdAsync(productVersionId, cancellationToken) is null)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.NotFound(ProductVersionNotFoundMessage);
        }

        var now = DateTime.UtcNow;
        var fileId = Guid.NewGuid();
        var fileLinkId = Guid.NewGuid();
        var originalFileName = Path.GetFileName(request.OriginalFileName.Trim());
        var generatedFileName = BuildGeneratedFileName(fileId, originalFileName);
        var objectName = BuildProductVersionObjectName(productVersionId, generatedFileName);
        var visibility = request.Visibility ?? FileVisibility.CUSTOMER_VISIBLE;

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
            ReferenceType = CatalogFileReferenceTypes.ProductVersion,
            ReferenceId = productVersionId,
            FileType = request.FileType,
            Visibility = visibility,
            Description = NormalizeOptional(request.Description),
            CreatedBy = currentUserId,
            CreatedAt = now
        };

        await _files.AddAsync(storedFile, cancellationToken);
        await _files.AddFileLinkAsync(fileLink, cancellationToken);
        await _files.SaveChangesAsync(cancellationToken);

        return ServiceResult<CatalogFileUploadResponseDto>.Created(
            new CatalogFileUploadResponseDto
            {
                FileId = fileId,
                FileLinkId = fileLinkId,
                ReferenceType = CatalogFileReferenceTypes.ProductVersion,
                ReferenceId = productVersionId,
                OriginalFileName = originalFileName,
                FileType = request.FileType,
                FileUrl = uploadResult.PublicUrl,
                MimeType = storedFile.MimeType,
                FileSizeBytes = request.FileSizeBytes,
                Visibility = visibility,
                UploadedBy = currentUserId,
                UploadedAt = now
            },
            "Product version file uploaded successfully.");
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

    private List<string> ValidateCatalogFileRequest(
        UploadCatalogFileRequestDto request,
        HashSet<FileType> allowedFileTypes)
    {
        var errors = new List<string>();
        if (request.Content == Stream.Null || !request.Content.CanRead)
        {
            errors.Add("File is required.");
        }

        if (string.IsNullOrWhiteSpace(request.OriginalFileName))
        {
            errors.Add("Original file name is required.");
        }

        if (request.FileSizeBytes <= 0)
        {
            errors.Add("File size must be greater than zero.");
        }

        var maxFileSize = ResolveMaxFileSize();
        if (request.FileSizeBytes > maxFileSize)
        {
            errors.Add($"File size must not exceed {maxFileSize} bytes.");
        }

        if (!allowedFileTypes.Contains(request.FileType))
        {
            errors.Add("File type is not allowed for this upload.");
        }

        var extension = Path.GetExtension(request.OriginalFileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions().Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("File extension is not allowed.");
        }

        var contentType = NormalizeContentType(request.ContentType);
        if (!AllowedMimeTypes().Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("File MIME type is not allowed.");
        }

        return errors;
    }

    private long ResolveMaxFileSize()
    {
        return _uploadSettings.MaxFileSizeBytes > 0
            ? _uploadSettings.MaxFileSizeBytes
            : _firebaseSettings.MaxFileSizeBytes;
    }

    private string[] AllowedExtensions()
    {
        return _uploadSettings.AllowedExtensions.Length == 0
            ? new FileUploadSettings().AllowedExtensions
            : _uploadSettings.AllowedExtensions;
    }

    private string[] AllowedMimeTypes()
    {
        return _uploadSettings.AllowedMimeTypes.Length == 0
            ? new FileUploadSettings().AllowedMimeTypes
            : _uploadSettings.AllowedMimeTypes;
    }

    private string BuildProductVersionObjectName(Guid productVersionId, string generatedFileName)
    {
        var prefix = string.IsNullOrWhiteSpace(_firebaseSettings.ProductVersionFilesPrefix)
            ? "product-versions"
            : _firebaseSettings.ProductVersionFilesPrefix.Trim().Trim('/');

        return $"{prefix}/{productVersionId:D}/{generatedFileName}";
    }

    private static string BuildGeneratedFileName(Guid fileId, string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        return $"{fileId:N}{extension}";
    }

    private static string? NormalizeExtension(string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        return extension.TrimStart('.').ToLowerInvariant();
    }

    private static string NormalizeContentType(string? contentType)
    {
        return string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType.Trim();
    }

    private static List<CatalogFileDto> ToCatalogFileList(
        IEnumerable<CatalogFileReadModel> files,
        bool customerVisibleOnly)
    {
        return files
            .Where(file =>
                file.Status == FileStatus.ACTIVE &&
                (!customerVisibleOnly || file.Visibility == FileVisibility.CUSTOMER_VISIBLE))
            .OrderByDescending(file => file.FileType == FileType.PRODUCT_PREVIEW)
            .ThenByDescending(file => file.UploadedAt)
            .Adapt<List<CatalogFileDto>>();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
