using FurniSpace.Application.Common;
using FurniSpace.Application.Common.LayoutAssets;
using FurniSpace.Application.DTOs.LayoutAssets;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using Microsoft.EntityFrameworkCore;
using static FurniSpace.Application.Constants.LayoutAssets.LayoutAssetServiceConstants;

namespace FurniSpace.Application.Services.LayoutAssets;

public sealed partial class LayoutAssetService
{
    public async Task<ServiceResult<CatalogFileUploadResponseDto>> UploadFileAsync(
        Guid layoutAssetId,
        Guid currentUserId,
        UploadCatalogFileRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (layoutAssetId == Guid.Empty)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.NotFound(LayoutAssetErrorCodes.NotFound);
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.Unauthorized("Authenticated account id is required.");
        }

        if (!LayoutAssetFileSummaryHelper.IsAllowedUploadFileType(request.FileType))
        {
            return ServiceResult<CatalogFileUploadResponseDto>.Failure(
                Error.BadRequest(
                    LayoutAssetErrorCodes.InvalidFileType,
                    "File type is not allowed for layout asset uploads."));
        }

        var validationErrors = CatalogFileUploadValidation.ValidateGeneralUpload(
            request,
            _uploadSettings,
            _firebaseSettings,
            AllowedLayoutAssetFileTypes);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.BadRequest(validationErrors);
        }

        if (await _layoutAssets.GetByIdAsync(layoutAssetId, cancellationToken) is null)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.NotFound(LayoutAssetErrorCodes.NotFound);
        }

        return await PersistUploadedFileAsync(layoutAssetId, currentUserId, request, cancellationToken);
    }

    public async Task<ServiceResult<IReadOnlyList<LayoutAssetFileDto>>> GetFilesAsync(
        Guid layoutAssetId,
        CancellationToken cancellationToken = default)
    {
        if (layoutAssetId == Guid.Empty)
        {
            return ServiceResult<IReadOnlyList<LayoutAssetFileDto>>.NotFound(LayoutAssetErrorCodes.NotFound);
        }

        if (await _layoutAssets.GetByIdAsync(layoutAssetId, cancellationToken) is null)
        {
            return ServiceResult<IReadOnlyList<LayoutAssetFileDto>>.NotFound(LayoutAssetErrorCodes.NotFound);
        }

        var files = await _files.GetCatalogFilesByReferencesAsync(
            CatalogFileReferenceTypes.LayoutAsset,
            [layoutAssetId],
            customerVisibleOnly: false,
            cancellationToken);

        return ServiceResult<IReadOnlyList<LayoutAssetFileDto>>.Success(
            LayoutAssetFileSummaryHelper.ToFileDtos(files),
            FilesRetrievedMessage);
    }

    public async Task<ServiceResult<LayoutAssetFilePrimaryResponseDto>> SetPrimaryFileAsync(
        Guid layoutAssetId,
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        if (layoutAssetId == Guid.Empty)
        {
            return ServiceResult<LayoutAssetFilePrimaryResponseDto>.NotFound(LayoutAssetErrorCodes.NotFound);
        }

        if (fileId == Guid.Empty)
        {
            return ServiceResult<LayoutAssetFilePrimaryResponseDto>.BadRequest("File id is required.");
        }

        if (await _layoutAssets.GetByIdAsync(layoutAssetId, cancellationToken) is null)
        {
            return ServiceResult<LayoutAssetFilePrimaryResponseDto>.NotFound(LayoutAssetErrorCodes.NotFound);
        }

        var selectedLink = await _files.GetFileLinkEntityAsync(
            CatalogFileReferenceTypes.LayoutAsset,
            layoutAssetId,
            fileId,
            cancellationToken);
        if (selectedLink is null)
        {
            return ServiceResult<LayoutAssetFilePrimaryResponseDto>.NotFound(LayoutAssetErrorCodes.FileNotFound);
        }

        if (!LayoutAssetFileSummaryHelper.IsPrimaryEligibleFileType(selectedLink.FileType))
        {
            return ServiceResult<LayoutAssetFilePrimaryResponseDto>.Failure(
                Error.BadRequest(
                    LayoutAssetErrorCodes.InvalidFileType,
                    "Only model, texture, or preview files can be set as primary."));
        }

        var links = await _files.GetFileLinkEntitiesByReferenceAsync(
            CatalogFileReferenceTypes.LayoutAsset,
            layoutAssetId,
            cancellationToken);

        await UnitOfWorkTransactions.ExecuteAsync(
            _unitOfWork,
            async ct =>
            {
                foreach (var link in links.Where(link => SharesPrimaryGroup(link.FileType, selectedLink.FileType ?? FileType.OTHER)))
                {
                    link.IsPrimary = link.FileId == fileId;
                }

                await _unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);

        return ServiceResult<LayoutAssetFilePrimaryResponseDto>.Success(
            new LayoutAssetFilePrimaryResponseDto
            {
                LayoutAssetId = layoutAssetId,
                FileId = fileId,
                FileLinkId = selectedLink.FileLinkId,
                FileType = selectedLink.FileType,
                IsPrimary = true
            },
            PrimaryFileUpdatedMessage);
    }

    public async Task<ServiceResult<LayoutAssetFileDto>> DeleteFileAsync(
        Guid layoutAssetId,
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        if (layoutAssetId == Guid.Empty)
        {
            return ServiceResult<LayoutAssetFileDto>.NotFound(LayoutAssetErrorCodes.NotFound);
        }

        if (fileId == Guid.Empty)
        {
            return ServiceResult<LayoutAssetFileDto>.BadRequest("File id is required.");
        }

        if (await _layoutAssets.GetByIdAsync(layoutAssetId, cancellationToken) is null)
        {
            return ServiceResult<LayoutAssetFileDto>.NotFound(LayoutAssetErrorCodes.NotFound);
        }

        var selectedLink = await _files.GetFileLinkEntityAsync(
            CatalogFileReferenceTypes.LayoutAsset,
            layoutAssetId,
            fileId,
            cancellationToken);
        if (selectedLink is null)
        {
            return ServiceResult<LayoutAssetFileDto>.NotFound(LayoutAssetErrorCodes.FileNotFound);
        }

        var file = await _files.GetByIdAsync(fileId, cancellationToken);
        if (file is null)
        {
            return ServiceResult<LayoutAssetFileDto>.NotFound(LayoutAssetErrorCodes.FileNotFound);
        }

        var deletedDto = new LayoutAssetFileDto
        {
            FileId = file.FileId,
            FileType = selectedLink.FileType,
            Url = file.FileUrl,
            FileName = file.OriginalFileName,
            MimeType = file.MimeType,
            IsPrimary = selectedLink.IsPrimary == true,
            DisplayOrder = selectedLink.DisplayOrder,
            Status = file.Status ?? FileStatus.ACTIVE
        };

        var fileLinks = await _files.GetFileLinkEntitiesByFileIdAsync(fileId, cancellationToken);
        var storagePath = file.StoragePath;

        await UnitOfWorkTransactions.ExecuteAsync(
            _unitOfWork,
            async ct =>
            {
                _files.RemoveFileLinks(fileLinks);
                _files.Remove(file);
                await _unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);

        await _storage.DeleteAsync(storagePath, cancellationToken);

        return ServiceResult<LayoutAssetFileDto>.Success(deletedDto, FileDeletedMessage);
    }

    private async Task<ServiceResult<CatalogFileUploadResponseDto>> PersistUploadedFileAsync(
        Guid layoutAssetId,
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
            "layout-assets",
            _firebaseSettings.LayoutAssetFilesPrefix,
            layoutAssetId,
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

        var existingLinks = await _files.GetFileLinkEntitiesByReferenceAsync(
            CatalogFileReferenceTypes.LayoutAsset,
            layoutAssetId,
            cancellationToken);
        var shouldSetPrimary = !existingLinks.Any(link => SharesPrimaryGroup(link.FileType, request.FileType) && link.IsPrimary == true);

        var fileLink = CatalogFileEntityFactory.CreateFileLink(new CatalogFileLinkCreationContext
        {
            FileLinkId = fileLinkId,
            FileId = fileId,
            ReferenceType = CatalogFileReferenceTypes.LayoutAsset,
            ReferenceId = layoutAssetId,
            FileType = request.FileType,
            Visibility = visibility,
            CreatedBy = currentUserId,
            CreatedAt = now,
            Description = request.Description,
            DisplayOrder = request.DisplayOrder
        });
        fileLink.IsPrimary = shouldSetPrimary;

        await _files.AddAsync(storedFile, cancellationToken);
        await _files.AddFileLinkAsync(fileLink, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DatabaseExceptionMapper.IsFileLinkUniqueViolation(exception))
        {
            await _storage.DeleteAsync(uploadResult.ObjectName, cancellationToken);
            return ServiceResult<CatalogFileUploadResponseDto>.Failure(
                Error.Conflict(
                    LayoutAssetErrorCodes.InvalidFileType,
                    "Layout asset file link already exists."));
        }

        return ServiceResult<CatalogFileUploadResponseDto>.Created(
            CatalogFileUploadResponseMapper.FromUpload(new CatalogFileUploadResponseContext
            {
                FileId = fileId,
                FileLinkId = fileLinkId,
                ReferenceType = CatalogFileReferenceTypes.LayoutAsset,
                ReferenceId = layoutAssetId,
                OriginalFileName = originalFileName,
                Request = request,
                UploadResult = uploadResult,
                StoredFile = storedFile,
                FileLink = fileLink,
                Visibility = visibility,
                CurrentUserId = currentUserId,
                UploadedAt = now
            }),
            FileUploadedMessage);
    }

    private static bool SharesPrimaryGroup(FileType? left, FileType right)
    {
        if (!left.HasValue)
        {
            return false;
        }

        if (left.Value == right)
        {
            return true;
        }

        return left is FileType.PREVIEW or FileType.PRODUCT_PREVIEW
            && right is FileType.PREVIEW or FileType.PRODUCT_PREVIEW;
    }
}
