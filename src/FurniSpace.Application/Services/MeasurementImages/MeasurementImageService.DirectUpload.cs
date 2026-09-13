using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Storage;
using FurniSpace.Application.Constants.Common;
using FurniSpace.Application.DTOs.MeasurementImages;
using FurniSpace.Application.DTOs.ProjectFiles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.ReadModels.ProjectSchedules;
using static FurniSpace.Application.Constants.MeasurementImages.MeasurementImageServiceConstants;

namespace FurniSpace.Application.Services.MeasurementImages;

public sealed partial class MeasurementImageService
{
    public async Task<ServiceResult<PrepareMeasurementImageUploadResponseDto>> PrepareMeasurementImageUploadAsync(
        Guid scheduleId,
        Guid currentUserId,
        PrepareMeasurementImageUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (scheduleId == Guid.Empty)
        {
            return ServiceResult<PrepareMeasurementImageUploadResponseDto>.BadRequest("Schedule id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<PrepareMeasurementImageUploadResponseDto>.Unauthorized();
        }

        var validationErrors = ValidatePrepareUploadMetadata(request);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<PrepareMeasurementImageUploadResponseDto>.BadRequest(validationErrors);
        }

        var accessError = await ValidateMeasurementImageUploadAccessAsync(
            scheduleId,
            currentUserId,
            request.ProjectAreaId,
            cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        var schedule = await _schedules.GetDetailAsync(scheduleId, cancellationToken);
        if (schedule is null)
        {
            return ServiceResult<PrepareMeasurementImageUploadResponseDto>.NotFound(ScheduleNotFoundMessage);
        }

        var roleName = await _files.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        var now = DateTime.UtcNow;
        var fileId = Guid.NewGuid();
        var scheduleFileLinkId = Guid.NewGuid();
        var originalFileName = Path.GetFileName(request.OriginalFileName.Trim());
        var generatedFileName = ProjectFileUploadSupport.BuildGeneratedFileName(fileId, originalFileName);
        var objectName = ProjectFileUploadSupport.BuildProjectObjectName(
            _firebaseSettings,
            schedule.ProjectId,
            generatedFileName);
        var contentType = ProjectFileUploadSupport.NormalizeContentType(request.ContentType);
        var visibility = ProjectFileUploadSupport.ResolveVisibility(
            request.Visibility,
            roleName!,
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

        var scheduleFileLink = new FileLink
        {
            FileLinkId = scheduleFileLinkId,
            FileId = fileId,
            ReferenceType = ProjectScheduleReferenceType,
            ReferenceId = scheduleId,
            FileType = FileType.SPACE_IMAGE,
            Visibility = visibility,
            Description = ProjectFileUploadSupport.NormalizeOptionalText(request.Note),
            CreatedBy = currentUserId,
            CreatedAt = now
        };

        FileLink? areaFileLink = null;
        if (request.ProjectAreaId.HasValue && request.ProjectAreaId.Value != Guid.Empty)
        {
            areaFileLink = new FileLink
            {
                FileLinkId = Guid.NewGuid(),
                FileId = fileId,
                ReferenceType = ProjectAreaReferenceType,
                ReferenceId = request.ProjectAreaId.Value,
                FileType = FileType.SPACE_IMAGE,
                Visibility = FileVisibility.STAFF_ONLY,
                CreatedBy = currentUserId,
                CreatedAt = now
            };
        }

        await ExecuteInTransactionAsync(
            async ct =>
            {
                await _files.AddAsync(storedFile, ct);
                await _files.AddFileLinkAsync(scheduleFileLink, ct);
                if (areaFileLink is not null)
                {
                    await _files.AddFileLinkAsync(areaFileLink, ct);
                }

                await _unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);

        try
        {
            var signedUpload = await _directUploadCoordinator.CreateSignedUploadUrlAsync(
                objectName,
                contentType,
                cancellationToken);

            return ServiceResult<PrepareMeasurementImageUploadResponseDto>.Created(
                new PrepareMeasurementImageUploadResponseDto
                {
                    FileId = fileId,
                    ScheduleId = scheduleId,
                    ProjectId = schedule.ProjectId,
                    UploadUrl = signedUpload.UploadUrl,
                    ContentType = signedUpload.ContentType,
                    ExpiresAt = signedUpload.ExpiresAt
                },
                "Measurement image upload URL created successfully.");
        }
        catch
        {
            await _directUploadCoordinator.DeleteObjectIfExistsAsync(objectName, cancellationToken);
            _files.Remove(storedFile);
            _files.RemoveFileLinks(areaFileLink is null ? [scheduleFileLink] : [scheduleFileLink, areaFileLink]);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ServiceResult<MeasurementImageUploadResponseDto>> CompleteMeasurementImageUploadAsync(
        Guid scheduleId,
        Guid currentUserId,
        CompleteMeasurementImageUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (scheduleId == Guid.Empty)
        {
            return ServiceResult<MeasurementImageUploadResponseDto>.BadRequest("Schedule id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<MeasurementImageUploadResponseDto>.Unauthorized();
        }

        if (request.FileId == Guid.Empty)
        {
            return ServiceResult<MeasurementImageUploadResponseDto>.BadRequest("File id is required.");
        }

        var schedule = await _schedules.GetDetailAsync(scheduleId, cancellationToken);
        if (schedule is null)
        {
            return ServiceResult<MeasurementImageUploadResponseDto>.NotFound(ScheduleNotFoundMessage);
        }

        var roleName = await _files.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return ServiceResult<MeasurementImageUploadResponseDto>.Forbidden(InactiveOrMissingRoleMessage);
        }

        var eligibilityError = ValidateCaptureEligibility(schedule);
        if (eligibilityError is not null)
        {
            return eligibilityError;
        }

        if (!CanCaptureMeasurementImage(schedule, currentUserId, roleName))
        {
            return ServiceResult<MeasurementImageUploadResponseDto>.Forbidden(
                "Only the assigned designer can upload measurement images for this schedule.");
        }

        var storedFile = await _files.GetByIdAsync(request.FileId, cancellationToken);
        if (storedFile is null)
        {
            return ServiceResult<MeasurementImageUploadResponseDto>.Failure(
                Error.NotFound(DirectFileUploadErrorCodes.UploadNotFound, "Measurement image upload not found."));
        }

        var fileLinks = await _files.GetFileLinkEntitiesByFileIdAsync(request.FileId, cancellationToken);
        var scheduleFileLink = fileLinks.FirstOrDefault(link =>
            string.Equals(link.ReferenceType, ProjectScheduleReferenceType, StringComparison.OrdinalIgnoreCase) &&
            link.ReferenceId == scheduleId);
        if (scheduleFileLink is null)
        {
            return ServiceResult<MeasurementImageUploadResponseDto>.Failure(
                Error.NotFound(DirectFileUploadErrorCodes.UploadNotFound, "Measurement image upload not found."));
        }

        if (storedFile.UploadedBy != currentUserId &&
            !IsAdmin(roleName))
        {
            return ServiceResult<MeasurementImageUploadResponseDto>.Failure(
                Error.Forbidden(
                    DirectFileUploadErrorCodes.UploadForbidden,
                    "You can only complete your own direct upload session."));
        }

        if (storedFile.Status == FileStatus.ACTIVE)
        {
            var areaLink = fileLinks.FirstOrDefault(link =>
                string.Equals(link.ReferenceType, ProjectAreaReferenceType, StringComparison.OrdinalIgnoreCase));

            return ServiceResult<MeasurementImageUploadResponseDto>.Success(
                BuildMeasurementUploadResponse(schedule, storedFile, scheduleFileLink, areaLink),
                "Measurement image uploaded successfully.");
        }

        if (storedFile.Status != FileStatus.PENDING)
        {
            return ServiceResult<MeasurementImageUploadResponseDto>.Failure(
                Error.Conflict(
                    DirectFileUploadErrorCodes.UploadNotPending,
                    "Measurement image upload is not pending completion."));
        }

        var finalizeResult = await _directUploadCoordinator.TryFinalizeAsync(storedFile, cancellationToken);
        if (finalizeResult.Status is not (200 or 201) || finalizeResult.Data is null)
        {
            return ServiceResult<MeasurementImageUploadResponseDto>.Failure(
                Error.Conflict(
                    finalizeResult.ErrorCode ?? DirectFileUploadErrorCodes.UploadObjectMissing,
                    finalizeResult.Message ?? "Direct upload finalization failed."));
        }

        var uploadResult = finalizeResult.Data;
        _directUploadCoordinator.ActivateStoredFile(storedFile, uploadResult);

        await ExecuteInTransactionAsync(
            async ct =>
            {
                _files.Update(storedFile);
                await _unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);

        var completedAreaLink = fileLinks.FirstOrDefault(link =>
            string.Equals(link.ReferenceType, ProjectAreaReferenceType, StringComparison.OrdinalIgnoreCase));

        return ServiceResult<MeasurementImageUploadResponseDto>.Success(
            BuildMeasurementUploadResponse(schedule, storedFile, scheduleFileLink, completedAreaLink),
            "Measurement image uploaded successfully.");
    }

    private async Task<ServiceResult<PrepareMeasurementImageUploadResponseDto>?> ValidateMeasurementImageUploadAccessAsync(
        Guid scheduleId,
        Guid currentUserId,
        Guid? projectAreaId,
        CancellationToken cancellationToken)
    {
        if (scheduleId == Guid.Empty)
        {
            return ServiceResult<PrepareMeasurementImageUploadResponseDto>.BadRequest("Schedule id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<PrepareMeasurementImageUploadResponseDto>.Unauthorized();
        }

        var roleName = await _files.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return ServiceResult<PrepareMeasurementImageUploadResponseDto>.Forbidden(InactiveOrMissingRoleMessage);
        }

        var schedule = await _schedules.GetDetailAsync(scheduleId, cancellationToken);
        if (schedule is null)
        {
            return ServiceResult<PrepareMeasurementImageUploadResponseDto>.NotFound(ScheduleNotFoundMessage);
        }

        var eligibilityError = ValidateCaptureEligibility(schedule);
        if (eligibilityError is not null)
        {
            return MapEligibilityFailure<PrepareMeasurementImageUploadResponseDto>(eligibilityError);
        }

        if (!CanCaptureMeasurementImage(schedule, currentUserId, roleName))
        {
            return ServiceResult<PrepareMeasurementImageUploadResponseDto>.Forbidden(
                "Only the assigned designer can upload measurement images for this schedule.");
        }

        var areaValidationError = await ValidateOptionalAreaLinkAsync(
            projectAreaId,
            schedule,
            currentUserId,
            roleName,
            cancellationToken);
        if (areaValidationError is not null)
        {
            return MapEligibilityFailure<PrepareMeasurementImageUploadResponseDto>(areaValidationError);
        }

        return null;
    }

    private static ServiceResult<T> MapEligibilityFailure<T>(ServiceResult<MeasurementImageUploadResponseDto> source)
    {
        return new ServiceResult<T>(source.Status, source.Message ?? "Request failed")
        {
            ErrorCode = source.ErrorCode,
            Errors = source.Errors
        };
    }

    private List<string> ValidatePrepareUploadMetadata(PrepareMeasurementImageUploadRequestDto request)
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

        var maxFileSize = ResolveMaxFileSize();
        if (request.FileSizeBytes > maxFileSize)
        {
            errors.Add($"File size must not exceed {maxFileSize} bytes.");
        }

        if (!string.IsNullOrWhiteSpace(request.OriginalFileName))
        {
            var extension = Path.GetExtension(request.OriginalFileName);
            if (string.IsNullOrWhiteSpace(extension) ||
                !AllowedImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add("Only image file extensions are allowed for measurement photos.");
            }
        }

        var contentType = ProjectFileUploadSupport.NormalizeContentType(request.ContentType);
        if (!AllowedImageMimeTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("Only image MIME types are allowed for measurement photos.");
        }

        return errors;
    }

    private static MeasurementImageUploadResponseDto BuildMeasurementUploadResponse(
        ProjectScheduleDetailReadModel schedule,
        StoredFile storedFile,
        FileLink scheduleFileLink,
        FileLink? areaFileLink)
    {
        return new MeasurementImageUploadResponseDto
        {
            File = BuildUploadResponse(
                schedule.ProjectId,
                schedule.ScheduleId,
                storedFile,
                scheduleFileLink,
                new StorageUploadResult
                {
                    ObjectName = storedFile.StoragePath,
                    PublicUrl = storedFile.FileUrl
                }),
            ScheduleId = schedule.ScheduleId,
            AreaLink = areaFileLink is null
                ? null
                : new MeasurementImageAreaLinkResponseDto
                {
                    ProjectAreaId = areaFileLink.ReferenceId,
                    FileId = storedFile.FileId,
                    FileLinkId = areaFileLink.FileLinkId
                }
        };
    }
}
