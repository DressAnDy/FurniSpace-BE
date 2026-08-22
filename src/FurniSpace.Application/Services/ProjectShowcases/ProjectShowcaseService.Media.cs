using FurniSpace.Application.Common;
using FurniSpace.Application.Constants.ProjectShowcases;
using FurniSpace.Application.DTOs.ProjectShowcases;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
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

        var showcase = await _showcases.GetForUpdateAsync(showcaseId, cancellationToken);
        if (showcase is null)
        {
            return ServiceResult<ProjectShowcaseMediaDto>.NotFound(ProjectShowcaseErrorCodes.NotFound);
        }

        var project = await _projects.GetByIdAsync(showcase.ProjectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectShowcaseMediaDto>.NotFound(ProjectShowcaseErrorCodes.NotFound);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanManageMedia(project, currentUserId, roleName))
        {
            return ServiceResult<ProjectShowcaseMediaDto>.Forbidden(ManageForbiddenMessage);
        }

        if (showcase.Status == ProjectShowcaseStatus.ARCHIVED)
        {
            return ServiceResult<ProjectShowcaseMediaDto>.Failure(
                Error.BadRequest(ProjectShowcaseErrorCodes.ArchivedReadOnly, "Archived showcases cannot be edited."));
        }

        var fileValidation = await ValidateShowcaseFileAsync(showcase.ProjectId, request.FileId, cancellationToken);
        if (fileValidation is not null)
        {
            return fileValidation;
        }

        var now = DateTime.UtcNow;
        var displayOrder = await _showcases.GetNextMediaDisplayOrderAsync(showcaseId, cancellationToken);
        if (request.SetAsCover)
        {
            await ClearCoverFlagsAsync(showcaseId, cancellationToken);
        }

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

        showcase.UpdatedAt = now;
        await _showcases.AddMediaAsync(media, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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

        var showcase = await _showcases.GetForUpdateAsync(showcaseId, cancellationToken);
        if (showcase is null)
        {
            return ServiceResult<ProjectShowcaseDto>.NotFound(ProjectShowcaseErrorCodes.NotFound);
        }

        var project = await _projects.GetByIdAsync(showcase.ProjectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectShowcaseDto>.NotFound(ProjectShowcaseErrorCodes.NotFound);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanManageMedia(project, currentUserId, roleName))
        {
            return ServiceResult<ProjectShowcaseDto>.Forbidden(ManageForbiddenMessage);
        }

        if (showcase.Status == ProjectShowcaseStatus.ARCHIVED)
        {
            return ServiceResult<ProjectShowcaseDto>.Failure(
                Error.BadRequest(ProjectShowcaseErrorCodes.ArchivedReadOnly, "Archived showcases cannot be edited."));
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
        var showcase = await _showcases.GetForUpdateAsync(showcaseId, cancellationToken);
        if (showcase is null)
        {
            return ServiceResult<ProjectShowcaseMediaDto>.NotFound(ProjectShowcaseErrorCodes.NotFound);
        }

        var project = await _projects.GetByIdAsync(showcase.ProjectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectShowcaseMediaDto>.NotFound(ProjectShowcaseErrorCodes.NotFound);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanManageMedia(project, currentUserId, roleName))
        {
            return ServiceResult<ProjectShowcaseMediaDto>.Forbidden(ManageForbiddenMessage);
        }

        if (showcase.Status == ProjectShowcaseStatus.ARCHIVED)
        {
            return ServiceResult<ProjectShowcaseMediaDto>.Failure(
                Error.BadRequest(ProjectShowcaseErrorCodes.ArchivedReadOnly, "Archived showcases cannot be edited."));
        }

        var media = await _showcases.GetMediaForUpdateAsync(showcaseId, mediaId, cancellationToken);
        if (media is null)
        {
            return ServiceResult<ProjectShowcaseMediaDto>.NotFound(ProjectShowcaseErrorCodes.MediaNotFound);
        }

        await ClearCoverFlagsAsync(showcaseId, cancellationToken);
        media.IsCover = true;
        media.UpdatedAt = DateTime.UtcNow;
        showcase.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
        var showcase = await _showcases.GetForUpdateAsync(showcaseId, cancellationToken);
        if (showcase is null)
        {
            return ServiceResult<ProjectShowcaseDto>.NotFound(ProjectShowcaseErrorCodes.NotFound);
        }

        var project = await _projects.GetByIdAsync(showcase.ProjectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectShowcaseDto>.NotFound(ProjectShowcaseErrorCodes.NotFound);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanManageMedia(project, currentUserId, roleName))
        {
            return ServiceResult<ProjectShowcaseDto>.Forbidden(ManageForbiddenMessage);
        }

        if (showcase.Status == ProjectShowcaseStatus.ARCHIVED)
        {
            return ServiceResult<ProjectShowcaseDto>.Failure(
                Error.BadRequest(ProjectShowcaseErrorCodes.ArchivedReadOnly, "Archived showcases cannot be edited."));
        }

        var media = await _showcases.GetMediaForUpdateAsync(showcaseId, mediaId, cancellationToken);
        if (media is null)
        {
            return ServiceResult<ProjectShowcaseDto>.NotFound(ProjectShowcaseErrorCodes.MediaNotFound);
        }

        await _showcases.RemoveMediaAsync(media, cancellationToken);
        showcase.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProjectShowcaseDto>.Success(
            await BuildDtoAsync(showcaseId, cancellationToken),
            MediaRemovedMessage);
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
