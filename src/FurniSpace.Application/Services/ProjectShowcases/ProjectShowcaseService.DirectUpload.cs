using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Storage;
using FurniSpace.Application.Constants.Common;
using FurniSpace.Application.DTOs.ProjectShowcases;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using Microsoft.EntityFrameworkCore;
using static FurniSpace.Application.Constants.ProjectShowcases.ProjectShowcaseServiceConstants;

namespace FurniSpace.Application.Services.ProjectShowcases;

public sealed partial class ProjectShowcaseService
{
    private static readonly string[] ShowcaseUploadImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    public async Task<ServiceResult<PrepareProjectShowcaseMediaUploadResponseDto>> PrepareMediaUploadAsync(
        Guid showcaseId,
        Guid currentUserId,
        PrepareProjectShowcaseMediaUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = ValidatePrepareMediaUploadMetadata(request);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<PrepareProjectShowcaseMediaUploadResponseDto>.BadRequest(validationErrors);
        }

        var accessError = await ValidateShowcaseMediaWriteAccessAsync<PrepareProjectShowcaseMediaUploadResponseDto>(
            showcaseId,
            currentUserId,
            cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        var showcase = await _showcases.GetForUpdateAsync(showcaseId, cancellationToken);
        if (showcase is null)
        {
            return ServiceResult<PrepareProjectShowcaseMediaUploadResponseDto>.NotFound(ProjectShowcaseErrorCodes.NotFound);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        var now = DateTime.UtcNow;
        var fileId = Guid.NewGuid();
        var fileLinkId = Guid.NewGuid();
        var originalFileName = Path.GetFileName(request.OriginalFileName.Trim());
        var generatedFileName = ProjectFileUploadSupport.BuildGeneratedFileName(fileId, originalFileName);
        var objectName = ProjectFileUploadSupport.BuildProjectObjectName(
            _firebaseSettings,
            showcase.ProjectId,
            generatedFileName);
        var contentType = ProjectFileUploadSupport.NormalizeContentType(request.ContentType);
        var visibility = ProjectFileUploadSupport.ResolveVisibility(
            null,
            roleName,
            ApplicationRoles.Customer);

        var storedFile = _directUploadCoordinator.CreatePendingStoredFile(
            new DirectUploadPendingFileRequest(
                fileId,
                currentUserId,
                originalFileName,
                generatedFileName,
                objectName,
                contentType,
                request.FileSizeBytes,
                now));

        var fileLink = ProjectFileUploadSupport.CreateProjectFileLink(
            new ProjectFileLinkCreationRequest(
                fileLinkId,
                fileId,
                showcase.ProjectId,
                FileType.PORTFOLIO_IMAGE,
                visibility,
                null,
                currentUserId,
                now));

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
            var signedUpload = await _directUploadCoordinator.CreateSignedUploadUrlAsync(
                objectName,
                contentType,
                cancellationToken);

            return ServiceResult<PrepareProjectShowcaseMediaUploadResponseDto>.Created(
                new PrepareProjectShowcaseMediaUploadResponseDto
                {
                    FileId = fileId,
                    ShowcaseId = showcaseId,
                    ProjectId = showcase.ProjectId,
                    UploadUrl = signedUpload.UploadUrl,
                    ContentType = signedUpload.ContentType,
                    ExpiresAt = signedUpload.ExpiresAt
                },
                "Showcase media upload URL created successfully.");
        }
        catch
        {
            await _directUploadCoordinator.DeleteObjectIfExistsAsync(objectName, cancellationToken);
            _files.Remove(storedFile);
            _files.RemoveFileLinks([fileLink]);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ServiceResult<ProjectShowcaseMediaDto>> CompleteMediaUploadAsync(
        Guid showcaseId,
        Guid currentUserId,
        CompleteProjectShowcaseMediaUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (showcaseId == Guid.Empty)
        {
            return ServiceResult<ProjectShowcaseMediaDto>.BadRequest("Showcase id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectShowcaseMediaDto>.Unauthorized("Authenticated account id is required.");
        }

        if (request.FileId == Guid.Empty)
        {
            return ServiceResult<ProjectShowcaseMediaDto>.BadRequest("File id is required.");
        }

        var accessError = await ValidateShowcaseMediaWriteAccessAsync<ProjectShowcaseMediaDto>(
            showcaseId,
            currentUserId,
            cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        var showcase = await _showcases.GetForUpdateAsync(showcaseId, cancellationToken);
        if (showcase is null)
        {
            return ServiceResult<ProjectShowcaseMediaDto>.NotFound(ProjectShowcaseErrorCodes.NotFound);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        var storedFile = await _files.GetByIdAsync(request.FileId, cancellationToken);
        if (storedFile is null)
        {
            return ServiceResult<ProjectShowcaseMediaDto>.Failure(
                Error.NotFound(DirectFileUploadErrorCodes.UploadNotFound, "Showcase media upload not found."));
        }

        var fileLinks = await _files.GetFileLinkEntitiesByFileIdAsync(request.FileId, cancellationToken);
        var projectLink = fileLinks.FirstOrDefault(link =>
            string.Equals(link.ReferenceType, ProjectFileUploadSupport.ProjectReferenceType, StringComparison.OrdinalIgnoreCase) &&
            link.ReferenceId == showcase.ProjectId);
        if (projectLink is null)
        {
            return ServiceResult<ProjectShowcaseMediaDto>.Failure(
                Error.NotFound(DirectFileUploadErrorCodes.UploadNotFound, "Showcase media upload not found."));
        }

        if (storedFile.UploadedBy != currentUserId &&
            !string.Equals(roleName, ApplicationRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<ProjectShowcaseMediaDto>.Failure(
                Error.Forbidden(
                    DirectFileUploadErrorCodes.UploadForbidden,
                    "You can only complete your own direct upload session."));
        }

        var showcaseMedia = await _showcases.GetMediaForUpdateAsync(showcaseId, cancellationToken);
        var existingMedia = showcaseMedia.FirstOrDefault(item => item.FileId == request.FileId);
        if (existingMedia is not null)
        {
            return ServiceResult<ProjectShowcaseMediaDto>.Success(
                BuildMediaDto(existingMedia, storedFile),
                MediaUploadedMessage);
        }

        if (storedFile.Status == FileStatus.PENDING)
        {
            var finalizeResult = await _directUploadCoordinator.TryFinalizeAsync(storedFile, cancellationToken);
            if (finalizeResult.Status is not (200 or 201) || finalizeResult.Data is null)
            {
                return ServiceResult<ProjectShowcaseMediaDto>.Failure(
                    Error.Conflict(
                        finalizeResult.ErrorCode ?? DirectFileUploadErrorCodes.UploadObjectMissing,
                        finalizeResult.Message ?? "Direct upload finalization failed."));
            }

            _directUploadCoordinator.ActivateStoredFile(storedFile, finalizeResult.Data);
        }
        else if (storedFile.Status != FileStatus.ACTIVE)
        {
            return ServiceResult<ProjectShowcaseMediaDto>.Failure(
                Error.Conflict(
                    DirectFileUploadErrorCodes.UploadNotPending,
                    "Showcase media upload is not pending completion."));
        }

        var now = DateTime.UtcNow;
        var displayOrder = await _showcases.GetNextMediaDisplayOrderAsync(showcaseId, cancellationToken);
        var media = new ProjectShowcaseMedia
        {
            ProjectShowcaseMediaId = Guid.NewGuid(),
            ProjectShowcaseId = showcaseId,
            FileId = request.FileId,
            MediaType = request.MediaType,
            Title = NormalizeOptionalText(request.Title),
            Caption = NormalizeOptionalText(request.Caption),
            IsCover = request.SetAsCover,
            DisplayOrder = displayOrder,
            CreatedAt = now,
            UpdatedAt = now
        };

        try
        {
            await UnitOfWorkTransactions.ExecuteAsync(
                _unitOfWork,
                async ct =>
                {
                    _files.Update(storedFile);

                    if (request.SetAsCover)
                    {
                        await ClearCoverFlagsAsync(showcaseId, ct);
                    }

                    await _showcases.AddMediaAsync(media, ct);
                    showcase.UpdatedAt = now;
                    await _unitOfWork.SaveChangesAsync(ct);
                },
                cancellationToken);
        }
        catch (DbUpdateException exception) when (DatabaseExceptionMapper.IsProjectShowcaseCoverUniqueViolation(exception))
        {
            return ServiceResult<ProjectShowcaseMediaDto>.Failure(
                Error.Conflict(
                    ProjectShowcaseErrorCodes.CoverConflict,
                    "Another showcase cover update conflicted. Please retry."));
        }

        return ServiceResult<ProjectShowcaseMediaDto>.Created(
            BuildMediaDto(media, storedFile),
            MediaUploadedMessage);
    }

    private List<string> ValidatePrepareMediaUploadMetadata(PrepareProjectShowcaseMediaUploadRequestDto request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.OriginalFileName))
        {
            errors.Add("Original file name is required.");
        }

        if (request.FileSizeBytes <= 0)
        {
            errors.Add("File size must be greater than zero.");
        }

        var maxFileSize = ResolveMaxUploadFileSize();
        if (request.FileSizeBytes > maxFileSize)
        {
            errors.Add($"File size must not exceed {maxFileSize} bytes.");
        }

        if (!string.IsNullOrWhiteSpace(request.OriginalFileName))
        {
            var extension = Path.GetExtension(request.OriginalFileName);
            if (string.IsNullOrWhiteSpace(extension) ||
                !ShowcaseUploadImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add("Only supported image file extensions are allowed for showcase media.");
            }
        }

        var contentType = ProjectFileUploadSupport.NormalizeContentType(request.ContentType);
        if (!AllowedImageMimeTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("Only supported image MIME types are allowed for showcase media.");
        }

        return errors;
    }

    private long ResolveMaxUploadFileSize()
    {
        return _uploadSettings.MaxFileSizeBytes > 0
            ? _uploadSettings.MaxFileSizeBytes
            : _firebaseSettings.MaxFileSizeBytes;
    }

    private static ProjectShowcaseMediaDto BuildMediaDto(ProjectShowcaseMedia media, StoredFile storedFile)
    {
        return new ProjectShowcaseMediaDto
        {
            ProjectShowcaseMediaId = media.ProjectShowcaseMediaId,
            FileId = media.FileId,
            MediaType = media.MediaType,
            Title = media.Title,
            Caption = media.Caption,
            IsCover = media.IsCover,
            DisplayOrder = media.DisplayOrder,
            FileUrl = storedFile.FileUrl,
            OriginalFileName = storedFile.OriginalFileName,
            MimeType = storedFile.MimeType
        };
    }
}
