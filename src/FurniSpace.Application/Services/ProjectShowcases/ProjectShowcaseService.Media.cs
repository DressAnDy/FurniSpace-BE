using FurniSpace.Application.Common;
using FurniSpace.Application.Constants.ProjectShowcases;
using FurniSpace.Application.DTOs.ProjectShowcases;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using Microsoft.EntityFrameworkCore;
using static FurniSpace.Application.Constants.ProjectShowcases.ProjectShowcaseServiceConstants;

namespace FurniSpace.Application.Services.ProjectShowcases;

public sealed partial class ProjectShowcaseService
{
    private static readonly string[] AllowedImageMimeTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp"
    ];

    public async Task<ServiceResult<ProjectShowcaseMediaDto>> AddMediaAsync(
        Guid showcaseId,
        Guid currentUserId,
        AddProjectShowcaseMediaRequestDto request,
        CancellationToken cancellationToken = default)
    {
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

        var fileValidation = await ValidateShowcaseFileAsync(showcase.ProjectId, request.FileId, cancellationToken);
        if (fileValidation is not null)
        {
            return fileValidation;
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
            if (request.SetAsCover)
            {
                await UnitOfWorkTransactions.ExecuteAsync(
                    _unitOfWork,
                    async ct =>
                    {
                        await ClearCoverFlagsAsync(showcaseId, ct);
                        await _showcases.AddMediaAsync(media, ct);
                        showcase.UpdatedAt = now;
                        await _unitOfWork.SaveChangesAsync(ct);
                    },
                    cancellationToken);
            }
            else
            {
                await _showcases.AddMediaAsync(media, cancellationToken);
                showcase.UpdatedAt = now;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
        catch (DbUpdateException exception) when (DatabaseExceptionMapper.IsProjectShowcaseCoverUniqueViolation(exception))
        {
            return ServiceResult<ProjectShowcaseMediaDto>.Failure(
                Error.Conflict(
                    ProjectShowcaseErrorCodes.CoverConflict,
                    "Another showcase cover update conflicted. Please retry."));
        }

        var linkedFile = await _files.GetProjectLinkedActiveFileAsync(showcase.ProjectId, request.FileId, cancellationToken);
        return ServiceResult<ProjectShowcaseMediaDto>.Created(
            BuildMediaDto(media, linkedFile),
            MediaAddedMessage);
    }

    public async Task<ServiceResult<ProjectShowcaseDto>> ReorderMediaAsync(
        Guid showcaseId,
        Guid currentUserId,
        ReorderProjectShowcaseMediaRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.MediaIds.Count == 0)
        {
            return ServiceResult<ProjectShowcaseDto>.BadRequest("Media ids are required.");
        }

        var accessError = await ValidateShowcaseMediaWriteAccessAsync<ProjectShowcaseDto>(
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
            return ServiceResult<ProjectShowcaseDto>.NotFound(ProjectShowcaseErrorCodes.NotFound);
        }

        var mediaItems = await _showcases.GetMediaForUpdateAsync(showcaseId, cancellationToken);
        if (request.MediaIds.Distinct().Count() != mediaItems.Count ||
            mediaItems.Any(item => !request.MediaIds.Contains(item.ProjectShowcaseMediaId)))
        {
            return ServiceResult<ProjectShowcaseDto>.BadRequest("Media reorder list must include all showcase media exactly once.");
        }

        var now = DateTime.UtcNow;
        for (var index = 0; index < request.MediaIds.Count; index++)
        {
            var media = mediaItems.First(item => item.ProjectShowcaseMediaId == request.MediaIds[index]);
            media.DisplayOrder = index;
            media.UpdatedAt = now;
        }

        showcase.UpdatedAt = now;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProjectShowcaseDto>.Success(
            await BuildDtoAsync(showcaseId, cancellationToken),
            MediaReorderedMessage);
    }

    public async Task<ServiceResult<ProjectShowcaseMediaDto>> SetCoverAsync(
        Guid showcaseId,
        Guid mediaId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
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

        var media = await _showcases.GetMediaForUpdateAsync(showcaseId, mediaId, cancellationToken);
        if (media is null)
        {
            return ServiceResult<ProjectShowcaseMediaDto>.NotFound(ProjectShowcaseErrorCodes.MediaNotFound);
        }

        var now = DateTime.UtcNow;
        try
        {
            await UnitOfWorkTransactions.ExecuteAsync(
                _unitOfWork,
                async ct =>
                {
                    await ClearCoverFlagsAsync(showcaseId, ct);
                    media.IsCover = true;
                    media.UpdatedAt = now;
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

        var linkedFile = await _files.GetProjectLinkedActiveFileAsync(showcase.ProjectId, media.FileId, cancellationToken);
        return ServiceResult<ProjectShowcaseMediaDto>.Success(
            BuildMediaDto(media, linkedFile),
            CoverUpdatedMessage);
    }

    public async Task<ServiceResult<ProjectShowcaseDto>> RemoveMediaAsync(
        Guid showcaseId,
        Guid mediaId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var accessError = await ValidateShowcaseMediaWriteAccessAsync<ProjectShowcaseDto>(
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
            return ServiceResult<ProjectShowcaseDto>.NotFound(ProjectShowcaseErrorCodes.NotFound);
        }

        var media = await _showcases.GetMediaForUpdateAsync(showcaseId, mediaId, cancellationToken);
        if (media is null)
        {
            return ServiceResult<ProjectShowcaseDto>.NotFound(ProjectShowcaseErrorCodes.MediaNotFound);
        }

        var replacement = media.IsCover
            ? SelectCoverReplacement(await _showcases.GetMediaForUpdateAsync(showcaseId, cancellationToken), mediaId)
            : null;
        var now = DateTime.UtcNow;

        await UnitOfWorkTransactions.ExecuteAsync(
            _unitOfWork,
            async ct =>
            {
                await _showcases.RemoveMediaAsync(media, ct);
                if (replacement is not null)
                {
                    replacement.IsCover = true;
                    replacement.UpdatedAt = now;
                }

                showcase.UpdatedAt = now;
                await _unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);

        return ServiceResult<ProjectShowcaseDto>.Success(
            await BuildDtoAsync(showcaseId, cancellationToken),
            MediaRemovedMessage);
    }

    private async Task<ServiceResult<T>?> ValidateShowcaseMediaWriteAccessAsync<T>(
        Guid showcaseId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var showcase = await _showcases.GetForUpdateAsync(showcaseId, cancellationToken);
        if (showcase is null)
        {
            return ServiceResult<T>.NotFound(ProjectShowcaseErrorCodes.NotFound);
        }

        var project = await _projects.GetByIdAsync(showcase.ProjectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<T>.NotFound(ProjectShowcaseErrorCodes.NotFound);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanManageMedia(project, currentUserId, roleName))
        {
            return ServiceResult<T>.Forbidden(ManageForbiddenMessage);
        }

        if (showcase.Status == ProjectShowcaseStatus.ARCHIVED)
        {
            return ServiceResult<T>.Failure(
                Error.BadRequest(ProjectShowcaseErrorCodes.ArchivedReadOnly, ArchivedReadOnlyMessage));
        }

        return null;
    }

    private async Task ClearCoverFlagsAsync(Guid showcaseId, CancellationToken cancellationToken)
    {
        var mediaItems = await _showcases.GetMediaForUpdateAsync(showcaseId, cancellationToken);
        foreach (var item in mediaItems.Where(item => item.IsCover))
        {
            item.IsCover = false;
            item.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static ProjectShowcaseMedia? SelectCoverReplacement(
        IReadOnlyList<ProjectShowcaseMedia> mediaItems,
        Guid excludedMediaId)
    {
        return mediaItems
            .Where(item => item.ProjectShowcaseMediaId != excludedMediaId)
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.CreatedAt)
            .ThenBy(item => item.ProjectShowcaseMediaId)
            .FirstOrDefault();
    }

    private async Task<ServiceResult<ProjectShowcaseMediaDto>?> ValidateShowcaseFileAsync(
        Guid projectId,
        Guid fileId,
        CancellationToken cancellationToken)
    {
        var linkedFile = await _files.GetProjectLinkedActiveFileAsync(projectId, fileId, cancellationToken);
        if (linkedFile is null)
        {
            return ServiceResult<ProjectShowcaseMediaDto>.Failure(
                Error.BadRequest(
                    ProjectShowcaseErrorCodes.FileNotInProject,
                    "File must belong to the same project."));
        }

        if (linkedFile.FileType is null || !AllowedShowcaseFileTypes.Contains(linkedFile.FileType.Value))
        {
            return ServiceResult<ProjectShowcaseMediaDto>.Failure(
                Error.BadRequest(
                    ProjectShowcaseErrorCodes.FileNotAllowed,
                    "File type is not allowed for portfolio showcase media."));
        }

        if (!AllowedImageMimeTypes.Contains(linkedFile.MimeType, StringComparer.OrdinalIgnoreCase))
        {
            return ServiceResult<ProjectShowcaseMediaDto>.Failure(
                Error.BadRequest(
                    ProjectShowcaseErrorCodes.FileNotAllowed,
                    "Only supported image files can be attached to a showcase."));
        }

        return null;
    }

    private static ProjectShowcaseMediaDto BuildMediaDto(
        ProjectShowcaseMedia media,
        ProjectLinkedFileReadModel? linkedFile)
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
            FileUrl = linkedFile?.FileUrl ?? string.Empty,
            OriginalFileName = linkedFile?.OriginalFileName ?? string.Empty,
            MimeType = linkedFile?.MimeType ?? string.Empty
        };
    }
}
