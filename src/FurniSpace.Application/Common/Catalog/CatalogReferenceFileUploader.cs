using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Repositories.IRepository;

namespace FurniSpace.Application.Common.Catalog;

public sealed class CatalogReferenceFileUploader
{
    private readonly IProjectFileRepository _files;
    private readonly IFileStorageService _storage;
    private readonly CatalogFileUploadRules _rules;

    public CatalogReferenceFileUploader(
        IProjectFileRepository files,
        IFileStorageService storage,
        CatalogFileUploadRules rules)
    {
        _files = files;
        _storage = storage;
        _rules = rules;
    }

    public async Task<ServiceResult<CatalogFileUploadResponseDto>> UploadAsync(
        UploadCatalogFileRequestDto request,
        Guid currentUserId,
        CatalogReferenceFileUploadOptions options,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.Unauthorized("Authenticated account id is required.");
        }

        var validationErrors = _rules.Validate(request, options.AllowedFileTypes);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.BadRequest(validationErrors);
        }

        var now = DateTime.UtcNow;
        var fileId = Guid.NewGuid();
        var fileLinkId = Guid.NewGuid();
        var originalFileName = Path.GetFileName(request.OriginalFileName.Trim());
        var generatedFileName = CatalogFileUploadRules.BuildGeneratedFileName(fileId, originalFileName);
        var objectName = _rules.BuildObjectName(
            options.StoragePrefixDefault,
            options.StoragePrefixConfigured,
            options.ReferenceId,
            generatedFileName);
        var visibility = request.Visibility ?? FileVisibility.CUSTOMER_VISIBLE;

        var uploadResult = await _storage.UploadAsync(
            new StorageUploadRequest
            {
                Content = request.Content,
                ObjectName = objectName,
                ContentType = CatalogFileUploadRules.NormalizeContentType(request.ContentType)
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
            MimeType = CatalogFileUploadRules.NormalizeContentType(request.ContentType),
            FileExtension = CatalogFileUploadRules.NormalizeExtension(originalFileName),
            FileSizeBytes = request.FileSizeBytes,
            Status = FileStatus.ACTIVE,
            UploadedAt = now
        };

        var fileLink = new FileLink
        {
            FileLinkId = fileLinkId,
            FileId = fileId,
            ReferenceType = options.ReferenceType,
            ReferenceId = options.ReferenceId,
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
                ReferenceType = options.ReferenceType,
                ReferenceId = options.ReferenceId,
                OriginalFileName = originalFileName,
                FileType = request.FileType,
                FileUrl = uploadResult.PublicUrl,
                MimeType = storedFile.MimeType,
                FileSizeBytes = request.FileSizeBytes,
                Visibility = visibility,
                UploadedBy = currentUserId,
                UploadedAt = now
            },
            options.SuccessMessage);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
