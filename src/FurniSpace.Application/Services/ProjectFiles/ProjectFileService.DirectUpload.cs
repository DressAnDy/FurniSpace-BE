using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Storage;
using FurniSpace.Application.Constants.Common;
using FurniSpace.Application.DTOs.ProjectFiles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using static FurniSpace.Application.Constants.ProjectFiles.ProjectFileServiceConstants;

namespace FurniSpace.Application.Services.ProjectFiles;

public sealed partial class ProjectFileService
{
    private sealed record LinkedFileUploadAccess(Guid ProjectId, string RoleName);

    private async Task<ServiceResult<PrepareProjectFileUploadResponseDto>> PrepareLinkedFileUploadAsync(
        Guid projectId,
        string referenceType,
        Guid referenceId,
        Guid currentUserId,
        PrepareProjectFileUploadRequestDto request,
        Func<Guid, Guid, CancellationToken, Task<ServiceResult<LinkedFileUploadAccess>?>> validateAccessAsync,
        CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
        {
            return ServiceResult<PrepareProjectFileUploadResponseDto>.BadRequest("Project id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<PrepareProjectFileUploadResponseDto>.Unauthorized("Authenticated account id is required.");
        }

        var validationErrors = ValidateUploadMetadata(request);
        if (referenceType == ProjectAreaReferenceType)
        {
            validationErrors.AddRange(ValidateProjectAreaFileMetadata(request));
        }

        if (validationErrors.Count > 0)
        {
            return ServiceResult<PrepareProjectFileUploadResponseDto>.BadRequest(validationErrors);
        }

        var accessResult = await validateAccessAsync(projectId, currentUserId, cancellationToken);
        if (accessResult is null)
        {
            return ServiceResult<PrepareProjectFileUploadResponseDto>.NotFound("Project not found.");
        }

        if (accessResult.Status is not (200 or 201) || accessResult.Data is null)
        {
            return MapAccessFailure<PrepareProjectFileUploadResponseDto>(accessResult);
        }

        var access = accessResult.Data;
        var now = DateTime.UtcNow;
        var fileId = Guid.NewGuid();
        var fileLinkId = Guid.NewGuid();
        var originalFileName = Path.GetFileName(request.OriginalFileName.Trim());
        var generatedFileName = BuildGeneratedFileName(fileId, originalFileName);
        var objectName = BuildProjectObjectName(projectId, generatedFileName);
        var contentType = NormalizeContentType(request.ContentType);
        var visibility = ResolveVisibility(request.Visibility, access.RoleName);
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
        var fileLink = CreateFileLink(
            fileLinkId,
            fileId,
            referenceType,
            referenceId,
            request,
            visibility,
            currentUserId,
            now);

        await ExecuteInTransactionAsync(
            async ct =>
            {
                await _projectFiles.AddAsync(storedFile, ct);
                await _projectFiles.AddFileLinkAsync(fileLink, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);

        try
        {
            var signedUpload = await _directUploadCoordinator.CreateSignedUploadUrlAsync(
                objectName,
                contentType,
                cancellationToken);

            return ServiceResult<PrepareProjectFileUploadResponseDto>.Created(
                new PrepareProjectFileUploadResponseDto
                {
                    FileId = fileId,
                    ProjectId = projectId,
                    UploadUrl = signedUpload.UploadUrl,
                    ContentType = signedUpload.ContentType,
                    ExpiresAt = signedUpload.ExpiresAt
                },
                "Project file upload URL created successfully.");
        }
        catch
        {
            await _directUploadCoordinator.DeleteObjectIfExistsAsync(objectName, cancellationToken);
            _projectFiles.Remove(storedFile);
            _projectFiles.RemoveFileLinks([fileLink]);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private async Task<ServiceResult<PrepareProjectAreaFileUploadResponseDto>> PrepareProjectAreaFileUploadInternalAsync(
        Guid projectAreaId,
        Guid currentUserId,
        PrepareProjectFileUploadRequestDto request,
        CancellationToken cancellationToken)
    {
        if (projectAreaId == Guid.Empty)
        {
            return ServiceResult<PrepareProjectAreaFileUploadResponseDto>.BadRequest("Project area id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<PrepareProjectAreaFileUploadResponseDto>.Unauthorized("Authenticated account id is required.");
        }

        var validationErrors = ValidateUploadMetadata(request);
        validationErrors.AddRange(ValidateProjectAreaFileMetadata(request));
        if (validationErrors.Count > 0)
        {
            return ServiceResult<PrepareProjectAreaFileUploadResponseDto>.BadRequest(validationErrors);
        }

        var project = await _projectFiles.GetReferenceProjectAccessAsync(
            ProjectAreaReferenceType,
            projectAreaId,
            cancellationToken);
        if (project is null)
        {
            return ServiceResult<PrepareProjectAreaFileUploadResponseDto>.NotFound("Project area not found.");
        }

        var prepareResult = await PrepareLinkedFileUploadAsync(
            project.ProjectId,
            ProjectAreaReferenceType,
            projectAreaId,
            currentUserId,
            request,
            (_, userId, ct) => ValidateProjectAreaFileUploadAccessAsync(projectAreaId, userId, ct),
            cancellationToken);

        if (prepareResult.Status is not (200 or 201) || prepareResult.Data is null)
        {
            return MapAccessFailure<PrepareProjectAreaFileUploadResponseDto>(prepareResult);
        }

        return ServiceResult<PrepareProjectAreaFileUploadResponseDto>.Created(
            new PrepareProjectAreaFileUploadResponseDto
            {
                FileId = prepareResult.Data.FileId,
                ProjectAreaId = projectAreaId,
                ProjectId = project.ProjectId,
                UploadUrl = prepareResult.Data.UploadUrl,
                ContentType = prepareResult.Data.ContentType,
                ExpiresAt = prepareResult.Data.ExpiresAt
            },
            "Project area file upload URL created successfully.");
    }

    private async Task<ServiceResult<ProjectFileUploadResponseDto>> CompleteLinkedFileUploadAsync(
        Guid projectId,
        string referenceType,
        Guid referenceId,
        Guid currentUserId,
        CompleteProjectFileUploadRequestDto request,
        Func<Guid, Guid, CancellationToken, Task<ServiceResult<LinkedFileUploadAccess>?>> validateAccessAsync,
        string successMessage,
        CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
        {
            return ServiceResult<ProjectFileUploadResponseDto>.BadRequest("Project id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectFileUploadResponseDto>.Unauthorized("Authenticated account id is required.");
        }

        if (request.FileId == Guid.Empty)
        {
            return ServiceResult<ProjectFileUploadResponseDto>.BadRequest("File id is required.");
        }

        var accessResult = await validateAccessAsync(projectId, currentUserId, cancellationToken);
        if (accessResult is null)
        {
            return ServiceResult<ProjectFileUploadResponseDto>.NotFound("Project not found.");
        }

        if (accessResult.Status is not (200 or 201) || accessResult.Data is null)
        {
            return MapAccessFailure<ProjectFileUploadResponseDto>(accessResult);
        }

        var access = accessResult.Data;
        var storedFile = await _projectFiles.GetByIdAsync(request.FileId, cancellationToken);
        if (storedFile is null)
        {
            return ServiceResult<ProjectFileUploadResponseDto>.Failure(
                Error.NotFound(DirectFileUploadErrorCodes.UploadNotFound, FileNotFoundMessage));
        }

        var fileLinks = await _projectFiles.GetFileLinkEntitiesByFileIdAsync(request.FileId, cancellationToken);
        var targetLink = fileLinks.FirstOrDefault(link =>
            string.Equals(link.ReferenceType, referenceType, StringComparison.OrdinalIgnoreCase) &&
            link.ReferenceId == referenceId);
        if (targetLink is null)
        {
            return ServiceResult<ProjectFileUploadResponseDto>.Failure(
                Error.NotFound(DirectFileUploadErrorCodes.UploadNotFound, FileNotFoundMessage));
        }

        if (storedFile.UploadedBy != currentUserId &&
            !string.Equals(access.RoleName, ApplicationRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<ProjectFileUploadResponseDto>.Failure(
                Error.Forbidden(
                    DirectFileUploadErrorCodes.UploadForbidden,
                    "You can only complete your own direct upload session."));
        }

        if (storedFile.Status == FileStatus.ACTIVE)
        {
            return ServiceResult<ProjectFileUploadResponseDto>.Success(
                BuildUploadResponse(
                    projectId,
                    referenceType,
                    referenceId,
                    storedFile,
                    targetLink,
                    new StorageUploadResult
                    {
                        Bucket = _firebaseSettings.Bucket,
                        ObjectName = storedFile.StoragePath,
                        PublicUrl = storedFile.FileUrl
                    }),
                successMessage);
        }

        if (storedFile.Status != FileStatus.PENDING)
        {
            return ServiceResult<ProjectFileUploadResponseDto>.Failure(
                Error.Conflict(
                    DirectFileUploadErrorCodes.UploadNotPending,
                    "Project file upload is not pending completion."));
        }

        var finalizeResult = await _directUploadCoordinator.TryFinalizeAsync(storedFile, cancellationToken);
        if (finalizeResult.Status is not (200 or 201) || finalizeResult.Data is null)
        {
            return ServiceResult<ProjectFileUploadResponseDto>.Failure(
                Error.Conflict(
                    finalizeResult.ErrorCode ?? DirectFileUploadErrorCodes.UploadObjectMissing,
                    finalizeResult.Message ?? "Direct upload finalization failed."));
        }

        var uploadResult = finalizeResult.Data;
        _directUploadCoordinator.ActivateStoredFile(storedFile, uploadResult);

        await ExecuteInTransactionAsync(
            async ct =>
            {
                _projectFiles.Update(storedFile);
                if (targetLink.IsPrimary == true)
                {
                    await ClearOtherPrimaryProjectAreaLinksAsync(targetLink, ct);
                }

                await _unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);

        return ServiceResult<ProjectFileUploadResponseDto>.Created(
            BuildUploadResponse(projectId, referenceType, referenceId, storedFile, targetLink, uploadResult),
            successMessage);
    }

    private async Task<ServiceResult<ProjectFileUploadResponseDto>> CompleteProjectAreaFileUploadInternalAsync(
        Guid projectAreaId,
        Guid currentUserId,
        CompleteProjectFileUploadRequestDto request,
        CancellationToken cancellationToken)
    {
        if (projectAreaId == Guid.Empty)
        {
            return ServiceResult<ProjectFileUploadResponseDto>.BadRequest("Project area id is required.");
        }

        var project = await _projectFiles.GetReferenceProjectAccessAsync(
            ProjectAreaReferenceType,
            projectAreaId,
            cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectFileUploadResponseDto>.NotFound("Project area not found.");
        }

        return await CompleteLinkedFileUploadAsync(
            project.ProjectId,
            ProjectAreaReferenceType,
            projectAreaId,
            currentUserId,
            request,
            (_, userId, ct) => ValidateProjectAreaFileUploadAccessAsync(projectAreaId, userId, ct),
            "Project area file uploaded successfully.",
            cancellationToken);
    }

    private async Task<ServiceResult<LinkedFileUploadAccess>?> ValidateProjectFileUploadAccessAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var project = await _projectFiles.GetProjectAccessAsync(projectId, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var roleName = await _projectFiles.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return ServiceResult<LinkedFileUploadAccess>.Forbidden(InactiveOrMissingRoleMessage);
        }

        if (!CanUpload(project.CustomerId, project.AssignedSalesId, project.AssignedDesignerId, currentUserId, roleName))
        {
            return ServiceResult<LinkedFileUploadAccess>.Forbidden(
                "You do not have access to upload files to this project.");
        }

        return ServiceResult<LinkedFileUploadAccess>.Success(
            new LinkedFileUploadAccess(projectId, roleName),
            string.Empty);
    }

    private async Task<ServiceResult<LinkedFileUploadAccess>?> ValidateProjectAreaFileUploadAccessAsync(
        Guid projectAreaId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var project = await _projectFiles.GetReferenceProjectAccessAsync(
            ProjectAreaReferenceType,
            projectAreaId,
            cancellationToken);
        if (project is null)
        {
            return null;
        }

        var roleName = await _projectFiles.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return ServiceResult<LinkedFileUploadAccess>.Forbidden(InactiveOrMissingRoleMessage);
        }

        if (!CanManageAreaFiles(project, currentUserId, roleName))
        {
            return ServiceResult<LinkedFileUploadAccess>.Forbidden(
                "You do not have access to upload files to this project area.");
        }

        return ServiceResult<LinkedFileUploadAccess>.Success(
            new LinkedFileUploadAccess(project.ProjectId, roleName),
            string.Empty);
    }

    private static ServiceResult<T> MapAccessFailure<T>(IServiceResult source)
    {
        return new ServiceResult<T>(source.Status, source.Message ?? "Request failed")
        {
            ErrorCode = source.ErrorCode,
            Errors = source.Errors
        };
    }

    private static List<string> ValidateProjectAreaFileMetadata(PrepareProjectFileUploadRequestDto request)
    {
        var errors = new List<string>();
        if (!IsSupportedProjectAreaFileType(request.FileType))
        {
            errors.Add("Project area file type is not supported.");
        }

        if (request.DisplayOrder.HasValue && request.DisplayOrder.Value < 0)
        {
            errors.Add("Display order must not be negative.");
        }

        return errors;
    }

    private static FileLink CreateFileLink(
        Guid fileLinkId,
        Guid fileId,
        string referenceType,
        Guid referenceId,
        PrepareProjectFileUploadRequestDto request,
        FileVisibility visibility,
        Guid currentUserId,
        DateTime now) =>
        new()
        {
            FileLinkId = fileLinkId,
            FileId = fileId,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            FileType = request.FileType,
            Visibility = visibility,
            IsPrimary = request.IsPrimary,
            DisplayOrder = request.DisplayOrder,
            Description = NormalizeOptional(request.Note),
            CreatedBy = currentUserId,
            CreatedAt = now
        };
}
