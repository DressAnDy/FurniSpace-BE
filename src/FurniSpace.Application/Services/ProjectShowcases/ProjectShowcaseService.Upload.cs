using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Storage;
using FurniSpace.Application.Constants.Common;
using FurniSpace.Application.DTOs.ProjectShowcases;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using static FurniSpace.Application.Constants.ProjectShowcases.ProjectShowcaseServiceConstants;

namespace FurniSpace.Application.Services.ProjectShowcases;

public sealed partial class ProjectShowcaseService
{
    private static readonly string[] ShowcaseUploadImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    public async Task<ServiceResult<ProjectShowcaseMediaDto>> UploadMediaAsync(
        Guid showcaseId,
        Guid currentUserId,
        UploadProjectShowcaseMediaRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = ValidateUploadMediaRequest(request);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<ProjectShowcaseMediaDto>.BadRequest(validationErrors);
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
        var now = DateTime.UtcNow;
        var fileId = Guid.NewGuid();
        var fileLinkId = Guid.NewGuid();
        var originalFileName = Path.GetFileName(request.OriginalFileName.Trim());
        var generatedFileName = ProjectFileUploadSupport.BuildGeneratedFileName(fileId, originalFileName);
        var objectName = ProjectFileUploadSupport.BuildProjectObjectName(
            _firebaseSettings,
            showcase.ProjectId,
            generatedFileName);
        var visibility = ProjectFileUploadSupport.ResolveVisibility(
            null,
            roleName,
            ApplicationRoles.Customer);

        var uploadResult = await _storage.UploadAsync(
            new StorageUploadRequest
            {
                Content = request.Content,
                ObjectName = objectName,
                ContentType = ProjectFileUploadSupport.NormalizeContentType(request.ContentType)
            },
            cancellationToken);

        var storedFile = ProjectFileUploadSupport.CreateStoredFile(
            new StoredFileCreationRequest(
                fileId,
                currentUserId,
                originalFileName,
                generatedFileName,
                uploadResult,
                request.ContentType,
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

        var displayOrder = await _showcases.GetNextMediaDisplayOrderAsync(showcaseId, cancellationToken);
        var media = new ProjectShowcaseMedia
        {
            ProjectShowcaseMediaId = Guid.NewGuid(),
            ProjectShowcaseId = showcaseId,
            FileId = fileId,
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
                    if (request.SetAsCover)
                    {
                        await ClearCoverFlagsAsync(showcaseId, ct);
                    }

                    await _files.AddAsync(storedFile, ct);
                    await _files.AddFileLinkAsync(fileLink, ct);
                    await _showcases.AddMediaAsync(media, ct);
                    showcase.UpdatedAt = now;
                    await _unitOfWork.SaveChangesAsync(ct);
                },
                cancellationToken);
        }
        catch (DbUpdateException exception) when (DatabaseExceptionMapper.IsProjectShowcaseCoverUniqueViolation(exception))
        {
            await _storage.DeleteAsync(uploadResult.ObjectName, cancellationToken);
            return ServiceResult<ProjectShowcaseMediaDto>.Failure(
                Error.Conflict(
                    ProjectShowcaseErrorCodes.CoverConflict,
                    "Another showcase cover update conflicted. Please retry."));
        }
        catch
        {
            await _storage.DeleteAsync(uploadResult.ObjectName, cancellationToken);
            throw;
        }

        return ServiceResult<ProjectShowcaseMediaDto>.Created(
            BuildMediaDto(media, storedFile),
            MediaUploadedMessage);
    }

    private List<string> ValidateUploadMediaRequest(UploadProjectShowcaseMediaRequestDto request)
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
