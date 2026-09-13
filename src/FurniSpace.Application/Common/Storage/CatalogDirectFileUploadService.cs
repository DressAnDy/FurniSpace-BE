using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Common;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;

namespace FurniSpace.Application.Common.Storage;

public sealed class CatalogDirectFileUploadService
{
    private const string FileNotFoundMessage = "Catalog file upload session was not found.";

    private readonly IProjectFileRepository _files;
    private readonly IUnitOfWork _unitOfWork;
    private readonly DirectFileUploadCoordinator _coordinator;
    private readonly FirebaseStorageSettings _firebaseSettings;

    public CatalogDirectFileUploadService(
        IProjectFileRepository files,
        IUnitOfWork unitOfWork,
        DirectFileUploadCoordinator coordinator,
        FirebaseStorageSettings firebaseSettings)
    {
        _files = files;
        _unitOfWork = unitOfWork;
        _coordinator = coordinator;
        _firebaseSettings = firebaseSettings;
    }

    public async Task<ServiceResult<PrepareDirectUploadResponseDto>> PrepareAsync(
        CatalogDirectUploadPrepareRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var fileId = Guid.NewGuid();
        var fileLinkId = Guid.NewGuid();
        var originalFileName = CatalogFileStorageHelpers.NormalizeOriginalFileName(request.Metadata.OriginalFileName);
        var generatedFileName = CatalogFileStorageHelpers.BuildGeneratedFileName(fileId, originalFileName);
        var storageObjectName = request.BuildStorageObjectName(fileId, generatedFileName);
        var contentType = CatalogFileStorageHelpers.NormalizeContentType(request.Metadata.ContentType);

        var storedFile = _coordinator.CreatePendingStoredFile(
            new DirectUploadPendingFileRequest(
                fileId,
                request.CurrentUserId,
                originalFileName,
                generatedFileName,
                storageObjectName,
                contentType,
                request.Metadata.FileSizeBytes,
                now));

        var fileLink = CatalogFileEntityFactory.CreateFileLink(new CatalogFileLinkCreationContext
        {
            FileLinkId = fileLinkId,
            FileId = fileId,
            ReferenceType = request.ReferenceType,
            ReferenceId = request.ReferenceId,
            FileType = request.FileType,
            Visibility = request.Visibility,
            CreatedBy = request.CurrentUserId,
            CreatedAt = now,
            Description = request.Metadata.Description,
            DisplayOrder = request.DisplayOrder
        });

        if (request.IsPrimary.HasValue)
        {
            fileLink.IsPrimary = request.IsPrimary.Value;
        }

        await UnitOfWorkTransactions.ExecuteAsync(
            _unitOfWork,
            async ct =>
            {
                await _files.AddAsync(storedFile, ct);
                await _files.AddFileLinkAsync(fileLink, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);

        try
        {
            var signedUpload = await _coordinator.CreateSignedUploadUrlAsync(
                storageObjectName,
                contentType,
                cancellationToken);

            return ServiceResult<PrepareDirectUploadResponseDto>.Created(
                new PrepareDirectUploadResponseDto
                {
                    FileId = fileId,
                    UploadUrl = signedUpload.UploadUrl,
                    ContentType = signedUpload.ContentType,
                    ExpiresAt = signedUpload.ExpiresAt
                },
                "Catalog file upload URL created successfully.");
        }
        catch
        {
            await _coordinator.DeleteObjectIfExistsAsync(storageObjectName, cancellationToken);
            _files.Remove(storedFile);
            _files.RemoveFileLinks([fileLink]);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ServiceResult<CatalogFileUploadResponseDto>> CompleteAsync(
        CatalogDirectUploadCompleteRequest request,
        Func<CatalogDirectUploadPostCompleteContext, CancellationToken, Task>? postCompleteAsync,
        CancellationToken cancellationToken = default)
    {
        if (request.FileId == Guid.Empty)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.BadRequest("File id is required.");
        }

        var storedFile = await _files.GetByIdAsync(request.FileId, cancellationToken);
        if (storedFile is null)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.Failure(
                Error.NotFound(DirectFileUploadErrorCodes.UploadNotFound, FileNotFoundMessage));
        }

        var fileLinks = await _files.GetFileLinkEntitiesByFileIdAsync(request.FileId, cancellationToken);
        var targetLink = fileLinks.FirstOrDefault(link =>
            string.Equals(link.ReferenceType, request.ReferenceType, StringComparison.OrdinalIgnoreCase) &&
            link.ReferenceId == request.ReferenceId);
        if (targetLink is null)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.Failure(
                Error.NotFound(DirectFileUploadErrorCodes.UploadNotFound, FileNotFoundMessage));
        }

        if (storedFile.UploadedBy != request.CurrentUserId)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.Failure(
                Error.Forbidden(
                    DirectFileUploadErrorCodes.UploadForbidden,
                    "You can only complete your own direct upload session."));
        }

        if (storedFile.Status == FileStatus.ACTIVE)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.Success(
                CatalogFileUploadResponseMapper.FromActivated(
                    storedFile,
                    targetLink,
                    request.ReferenceType,
                    request.ReferenceId,
                    new StorageUploadResult
                    {
                        Bucket = _firebaseSettings.Bucket,
                        ObjectName = storedFile.StoragePath,
                        PublicUrl = storedFile.FileUrl
                    }),
                request.SuccessMessage);
        }

        if (storedFile.Status != FileStatus.PENDING)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.Failure(
                Error.Conflict(
                    DirectFileUploadErrorCodes.UploadNotPending,
                    "Catalog file upload is not pending completion."));
        }

        var finalizeResult = await _coordinator.TryFinalizeAsync(storedFile, cancellationToken);
        if (finalizeResult.Status is not (200 or 201) || finalizeResult.Data is null)
        {
            return ServiceResult<CatalogFileUploadResponseDto>.Failure(
                Error.Conflict(
                    finalizeResult.ErrorCode ?? DirectFileUploadErrorCodes.UploadObjectMissing,
                    finalizeResult.Message ?? "Direct upload finalization failed."));
        }

        var uploadResult = finalizeResult.Data;
        _coordinator.ActivateStoredFile(storedFile, uploadResult);

        try
        {
            await UnitOfWorkTransactions.ExecuteAsync(
                _unitOfWork,
                async ct =>
                {
                    _files.Update(storedFile);
                    if (postCompleteAsync is not null)
                    {
                        await postCompleteAsync(
                            new CatalogDirectUploadPostCompleteContext(storedFile, targetLink),
                            ct);
                    }

                    await _unitOfWork.SaveChangesAsync(ct);
                },
                cancellationToken);
        }
        catch
        {
            await _coordinator.DeleteObjectIfExistsAsync(storedFile.StoragePath, cancellationToken);
            throw;
        }

        return ServiceResult<CatalogFileUploadResponseDto>.Created(
            CatalogFileUploadResponseMapper.FromActivated(
                storedFile,
                targetLink,
                request.ReferenceType,
                request.ReferenceId,
                uploadResult),
            request.SuccessMessage);
    }
}

public sealed record CatalogDirectUploadPrepareRequest(
    Guid CurrentUserId,
    UploadCatalogFileRequestDto Metadata,
    string ReferenceType,
    Guid ReferenceId,
    Func<Guid, string, string> BuildStorageObjectName,
    FileType FileType,
    FileVisibility Visibility,
    int? DisplayOrder = null,
    bool? IsPrimary = null);

public sealed record CatalogDirectUploadCompleteRequest(
    Guid CurrentUserId,
    Guid FileId,
    string ReferenceType,
    Guid ReferenceId,
    string SuccessMessage = "Catalog file uploaded successfully.");

public sealed record CatalogDirectUploadPostCompleteContext(
    StoredFile StoredFile,
    FileLink FileLink);
