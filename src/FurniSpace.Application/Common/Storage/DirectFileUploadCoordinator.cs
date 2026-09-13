using FurniSpace.Application.Common;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;

namespace FurniSpace.Application.Common.Storage;

public sealed class DirectFileUploadCoordinator
{
    private readonly IFileStorageService _storage;
    private readonly IDirectFileUploadStorageService _directUploadStorage;
    private readonly FirebaseStorageSettings _firebaseSettings;

    public DirectFileUploadCoordinator(
        IFileStorageService storage,
        IDirectFileUploadStorageService directUploadStorage,
        FirebaseStorageSettings firebaseSettings)
    {
        _storage = storage;
        _directUploadStorage = directUploadStorage;
        _firebaseSettings = firebaseSettings;
    }

    public StoredFile CreatePendingStoredFile(DirectUploadPendingFileRequest request)
    {
        return new StoredFile
        {
            FileId = request.FileId,
            UploadedBy = request.UploadedBy,
            OriginalFileName = request.OriginalFileName,
            StoredFileName = request.GeneratedFileName,
            FileUrl = string.Empty,
            StoragePath = request.StoragePath,
            MimeType = ProjectFileUploadSupport.NormalizeContentType(request.ContentType),
            FileExtension = ProjectFileUploadSupport.NormalizeExtension(request.OriginalFileName),
            FileSizeBytes = request.FileSizeBytes,
            Status = FileStatus.PENDING,
            UploadedAt = request.UploadedAt
        };
    }

    public async Task<StorageSignedUploadResult> CreateSignedUploadUrlAsync(
        string objectName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var normalizedContentType = ProjectFileUploadSupport.NormalizeContentType(contentType);
        return await _directUploadStorage.CreateSignedUploadUrlAsync(
            new StorageSignedUploadRequest
            {
                ObjectName = objectName,
                ContentType = normalizedContentType,
                Expiration = TimeSpan.FromMinutes(Math.Clamp(_firebaseSettings.UploadSignedUrlExpirationMinutes, 1, 60))
            },
            cancellationToken);
    }

    public async Task<ServiceResult<StorageUploadResult>> TryFinalizeAsync(
        StoredFile storedFile,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var uploadResult = await _directUploadStorage.FinalizeDirectUploadAsync(
                new StorageDirectUploadFinalizeRequest
                {
                    ObjectName = storedFile.StoragePath,
                    ContentType = storedFile.MimeType,
                    ExpectedSizeBytes = storedFile.FileSizeBytes
                },
                cancellationToken);

            return ServiceResult<StorageUploadResult>.Success(uploadResult, string.Empty);
        }
        catch (InvalidOperationException exception)
        {
            return MapFailure<StorageUploadResult>(exception);
        }
    }

    public void ActivateStoredFile(StoredFile storedFile, StorageUploadResult uploadResult)
    {
        storedFile.Status = FileStatus.ACTIVE;
        storedFile.FileUrl = uploadResult.PublicUrl;
        storedFile.UploadedAt = DateTime.UtcNow;
    }

    public async Task DeleteObjectIfExistsAsync(string objectName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return;
        }

        try
        {
            await _storage.DeleteAsync(objectName, cancellationToken);
        }
        catch
        {
            // Best-effort cleanup after failed prepare.
        }
    }

    public static ServiceResult<T> MapFailure<T>(InvalidOperationException exception)
    {
        var message = exception.Message;
        if (message.Contains("was not found", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<T>.Failure(
                Error.Conflict(DirectFileUploadErrorCodes.UploadObjectMissing, message));
        }

        if (message.Contains("size mismatch", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<T>.Failure(
                Error.Conflict(DirectFileUploadErrorCodes.UploadSizeMismatch, message));
        }

        if (message.Contains("content type mismatch", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<T>.Failure(
                Error.Conflict(DirectFileUploadErrorCodes.UploadContentTypeMismatch, message));
        }

        return ServiceResult<T>.Failure(
            Error.Conflict(DirectFileUploadErrorCodes.UploadObjectMissing, message));
    }
}

public sealed record DirectUploadPendingFileRequest(
    Guid FileId,
    Guid UploadedBy,
    string OriginalFileName,
    string GeneratedFileName,
    string StoragePath,
    string ContentType,
    long FileSizeBytes,
    DateTime UploadedAt);
