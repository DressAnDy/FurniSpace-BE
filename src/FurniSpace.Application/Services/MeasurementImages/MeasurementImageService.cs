using FurniSpace.Application.Common;
using FurniSpace.Application.Common.MeasurementImages;
using FurniSpace.Application.Common.Storage;
using FurniSpace.Application.Constants.Common;
using static FurniSpace.Application.Constants.MeasurementImages.MeasurementImageServiceConstants;
using FurniSpace.Application.DTOs.MeasurementImages;
using FurniSpace.Application.DTOs.ProjectFiles;
using FurniSpace.Application.Interfaces.MeasurementImages;
using FurniSpace.Application.Interfaces.Search;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using FurniSpace.Infrastructure.ReadModels.ProjectSchedules;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;

namespace FurniSpace.Application.Services.MeasurementImages;

public sealed class MeasurementImageService : IMeasurementImageService
{
    private static readonly string[] AllowedImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private static readonly string[] AllowedImageMimeTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp"
    ];

    private readonly IProjectScheduleRepository _schedules;
    private readonly IProjectFileRepository _files;
    private readonly IUnitOfWork _unitOfWork;
    private readonly FileUploadSettings _uploadSettings;
    private readonly FirebaseStorageSettings _firebaseSettings;
    private readonly IProjectFileSearchIndexer? _projectFileSearchIndexer;

    public MeasurementImageService(
        IProjectScheduleRepository schedules,
        IProjectFileRepository files,
        MeasurementImageServiceDependencies dependencies)
    {
        _schedules = schedules;
        _files = files;
        _unitOfWork = dependencies.UnitOfWork;
        _uploadSettings = dependencies.UploadSettings;
        _firebaseSettings = dependencies.FirebaseSettings;
        _projectFileSearchIndexer = dependencies.ProjectFileSearchIndexer;
    }

    public async Task<ServiceResult<ProjectFileUploadResponseDto>> RegisterMeasurementImageAsync(
        Guid scheduleId,
        Guid currentUserId,
        RegisterMeasurementImageRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (scheduleId == Guid.Empty)
        {
            return ServiceResult<ProjectFileUploadResponseDto>.BadRequest("Schedule id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectFileUploadResponseDto>.Unauthorized();
        }

        var validationErrors = ValidateRegisterRequest(request);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<ProjectFileUploadResponseDto>.BadRequest(validationErrors);
        }

        var roleName = await _files.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return ServiceResult<ProjectFileUploadResponseDto>.Forbidden(InactiveOrMissingRoleMessage);
        }

        var schedule = await _schedules.GetDetailAsync(scheduleId, cancellationToken);
        if (schedule is null)
        {
            return ServiceResult<ProjectFileUploadResponseDto>.NotFound(ScheduleNotFoundMessage);
        }

        var eligibilityError = ValidateCaptureEligibility(schedule);
        if (eligibilityError is not null)
        {
            return eligibilityError;
        }

        if (!CanCaptureMeasurementImage(schedule, currentUserId, roleName))
        {
            return ServiceResult<ProjectFileUploadResponseDto>.Forbidden(
                "Only the assigned designer can register measurement images for this schedule.");
        }

        if (!TryParseProjectStoragePath(
                request.StoragePath,
                schedule.ProjectId,
                out var fileId,
                out var generatedFileName))
        {
            return BadRequestUpload(
                MeasurementImageErrorCodes.StoragePathInvalid,
                "Storage path is invalid for this project.");
        }

        if (await _files.ExistsByStoragePathAsync(request.StoragePath, cancellationToken))
        {
            return ServiceResult<ProjectFileUploadResponseDto>.Failure(
                Error.Conflict(
                    MeasurementImageErrorCodes.StoragePathDuplicate,
                    "This storage path is already registered."));
        }

        var now = DateTime.UtcNow;
        var originalFileName = Path.GetFileName(request.OriginalFileName.Trim());
        var visibility = ProjectFileUploadSupport.ResolveVisibility(
            request.Visibility,
            roleName,
            ApplicationRoles.Customer);
        var fileLinkId = Guid.NewGuid();
        var storedFile = new StoredFile
        {
            FileId = fileId,
            UploadedBy = currentUserId,
            OriginalFileName = originalFileName,
            StoredFileName = generatedFileName,
            FileUrl = request.PublicUrl.Trim(),
            StoragePath = request.StoragePath.Trim(),
            MimeType = ProjectFileUploadSupport.NormalizeContentType(request.ContentType),
            FileExtension = ProjectFileUploadSupport.NormalizeExtension(originalFileName),
            FileSizeBytes = request.FileSizeBytes,
            Status = FileStatus.ACTIVE,
            UploadedAt = now
        };
        var fileLink = new FileLink
        {
            FileLinkId = fileLinkId,
            FileId = fileId,
            ReferenceType = ProjectScheduleReferenceType,
            ReferenceId = scheduleId,
            FileType = FileType.SPACE_IMAGE,
            Visibility = visibility,
            Description = ProjectFileUploadSupport.NormalizeOptionalText(request.Note),
            CreatedBy = currentUserId,
            CreatedAt = now
        };

        await ExecuteInTransactionAsync(
            async ct =>
            {
                await _files.AddAsync(storedFile, ct);
                await _files.AddFileLinkAsync(fileLink, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);

        await SyncProjectFileIndexAsync(fileId, cancellationToken);

        return ServiceResult<ProjectFileUploadResponseDto>.Created(
            new ProjectFileUploadResponseDto
            {
                FileId = storedFile.FileId,
                FileLinkId = fileLink.FileLinkId,
                ProjectId = schedule.ProjectId,
                ReferenceType = ProjectScheduleReferenceType,
                ReferenceId = scheduleId,
                OriginalFileName = storedFile.OriginalFileName,
                FileName = storedFile.StoredFileName,
                FileType = FileType.SPACE_IMAGE,
                MimeType = storedFile.MimeType,
                FileSize = storedFile.FileSizeBytes,
                StoragePath = storedFile.StoragePath,
                PublicUrl = storedFile.FileUrl,
                Visibility = visibility,
                UploadedBy = storedFile.UploadedBy,
                UploadedAt = storedFile.UploadedAt
            },
            "Measurement image registered successfully.");
    }

    public async Task<ServiceResult<MeasurementImageGalleryResponseDto>> GetProjectMeasurementImagesAsync(
        Guid projectId,
        Guid currentUserId,
        MeasurementImageGalleryQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            return ServiceResult<MeasurementImageGalleryResponseDto>.BadRequest("Project id is required.");
        }

        var accessError = await ValidateGalleryAccessAsync<MeasurementImageGalleryResponseDto>(
            projectId,
            currentUserId,
            query,
            cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        var roleName = await _files.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        var page = await _files.GetMeasurementImageGalleryAsync(
            BuildGalleryQuery(projectId, query, roleName, currentUserId),
            cancellationToken);

        return BuildGallerySuccess(page, query, "Project measurement images retrieved successfully.");
    }

    public async Task<ServiceResult<MeasurementImageGalleryResponseDto>> GetScheduleMeasurementImagesAsync(
        Guid scheduleId,
        Guid currentUserId,
        MeasurementImageGalleryQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (scheduleId == Guid.Empty)
        {
            return ServiceResult<MeasurementImageGalleryResponseDto>.BadRequest("Schedule id is required.");
        }

        var schedule = await _schedules.GetDetailAsync(scheduleId, cancellationToken);
        if (schedule is null)
        {
            return ServiceResult<MeasurementImageGalleryResponseDto>.NotFound(ScheduleNotFoundMessage);
        }

        var accessError = await ValidateGalleryAccessAsync<MeasurementImageGalleryResponseDto>(
            schedule.ProjectId,
            currentUserId,
            query,
            cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        if (query.ScheduleId.HasValue && query.ScheduleId.Value != scheduleId)
        {
            return ServiceResult<MeasurementImageGalleryResponseDto>.Failure(
                Error.BadRequest(
                    MeasurementImageErrorCodes.ScheduleProjectMismatch,
                    "Schedule filter does not match the requested schedule."));
        }

        var roleName = await _files.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        var galleryQuery = BuildGalleryQuery(schedule.ProjectId, query, roleName, currentUserId);
        galleryQuery = new MeasurementImageGalleryQueryReadModel
        {
            ProjectId = galleryQuery.ProjectId,
            ScheduleId = scheduleId,
            ProjectAreaId = galleryQuery.ProjectAreaId,
            Assigned = galleryQuery.Assigned,
            CustomerVisibleOnly = galleryQuery.CustomerVisibleOnly,
            CustomerAccountId = galleryQuery.CustomerAccountId,
            Page = galleryQuery.Page,
            Limit = galleryQuery.Limit
        };

        var page = await _files.GetMeasurementImageGalleryAsync(galleryQuery, cancellationToken);
        return BuildGallerySuccess(page, query, "Schedule measurement images retrieved successfully.");
    }

    public async Task<ServiceResult<MeasurementImageGalleryResponseDto>> GetProjectAreaMeasurementImagesAsync(
        Guid projectAreaId,
        Guid currentUserId,
        MeasurementImageGalleryQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (projectAreaId == Guid.Empty)
        {
            return ServiceResult<MeasurementImageGalleryResponseDto>.BadRequest("Project area id is required.");
        }

        var project = await _files.GetReferenceProjectAccessAsync(
            ProjectAreaReferenceType,
            projectAreaId,
            cancellationToken);
        if (project is null)
        {
            return ServiceResult<MeasurementImageGalleryResponseDto>.NotFound(ProjectAreaNotFoundMessage);
        }

        var accessError = await ValidateGalleryAccessAsync<MeasurementImageGalleryResponseDto>(
            project.ProjectId,
            currentUserId,
            query,
            cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        var paginationErrors = ValidatePagination(query.Page, query.Limit);
        if (paginationErrors.Count > 0)
        {
            return ServiceResult<MeasurementImageGalleryResponseDto>.BadRequest(paginationErrors);
        }

        var roleName = await _files.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        var page = await _files.GetMeasurementImageGalleryAsync(
            new MeasurementImageGalleryQueryReadModel
            {
                ProjectAreaId = projectAreaId,
                CustomerVisibleOnly = IsCustomer(roleName),
                CustomerAccountId = IsCustomer(roleName) ? currentUserId : null,
                Page = query.Page,
                Limit = query.Limit
            },
            cancellationToken);

        return BuildGallerySuccess(page, query, "Project area measurement images retrieved successfully.");
    }

    public async Task<ServiceResult<MeasurementImageAreaLinkResponseDto>> LinkMeasurementImageToAreaAsync(
        Guid projectAreaId,
        Guid fileId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (projectAreaId == Guid.Empty || fileId == Guid.Empty)
        {
            return ServiceResult<MeasurementImageAreaLinkResponseDto>.BadRequest(
                "Project area id and file id are required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<MeasurementImageAreaLinkResponseDto>.Unauthorized();
        }

        var roleName = await _files.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return ServiceResult<MeasurementImageAreaLinkResponseDto>.Forbidden(InactiveOrMissingRoleMessage);
        }

        var project = await _files.GetReferenceProjectAccessAsync(
            ProjectAreaReferenceType,
            projectAreaId,
            cancellationToken);
        if (project is null)
        {
            return ServiceResult<MeasurementImageAreaLinkResponseDto>.NotFound(ProjectAreaNotFoundMessage);
        }

        if (!CanManageAreaFiles(project, currentUserId, roleName))
        {
            return ServiceResult<MeasurementImageAreaLinkResponseDto>.Forbidden(
                "You do not have access to manage measurement images for this project area.");
        }

        if (!await _files.HasMeasurementScheduleLinkInProjectAsync(fileId, project.ProjectId, cancellationToken))
        {
            return ServiceResult<MeasurementImageAreaLinkResponseDto>.Failure(
                Error.BadRequest(
                    MeasurementImageErrorCodes.NotMeasurementImage,
                    "File is not a measurement image for this project."));
        }

        var existingLink = await _files.GetFileLinkEntityAsync(
            ProjectAreaReferenceType,
            projectAreaId,
            fileId,
            cancellationToken);
        if (existingLink is not null)
        {
            return ServiceResult<MeasurementImageAreaLinkResponseDto>.Failure(
                Error.Conflict(
                    MeasurementImageErrorCodes.AreaLinkExists,
                    "Measurement image is already linked to this project area."));
        }

        var now = DateTime.UtcNow;
        var fileLinkId = Guid.NewGuid();
        var fileLink = new FileLink
        {
            FileLinkId = fileLinkId,
            FileId = fileId,
            ReferenceType = ProjectAreaReferenceType,
            ReferenceId = projectAreaId,
            FileType = FileType.SPACE_IMAGE,
            Visibility = FileVisibility.STAFF_ONLY,
            CreatedBy = currentUserId,
            CreatedAt = now
        };

        await ExecuteInTransactionAsync(
            async ct =>
            {
                await _files.AddFileLinkAsync(fileLink, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);

        return ServiceResult<MeasurementImageAreaLinkResponseDto>.Created(
            new MeasurementImageAreaLinkResponseDto
            {
                ProjectAreaId = projectAreaId,
                FileId = fileId,
                FileLinkId = fileLinkId
            },
            "Measurement image linked to project area successfully.");
    }

    public async Task<ServiceResult<MeasurementImageAreaLinkResponseDto>> UnlinkMeasurementImageFromAreaAsync(
        Guid projectAreaId,
        Guid fileId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (projectAreaId == Guid.Empty || fileId == Guid.Empty)
        {
            return ServiceResult<MeasurementImageAreaLinkResponseDto>.BadRequest(
                "Project area id and file id are required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<MeasurementImageAreaLinkResponseDto>.Unauthorized();
        }

        var roleName = await _files.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return ServiceResult<MeasurementImageAreaLinkResponseDto>.Forbidden(InactiveOrMissingRoleMessage);
        }

        var project = await _files.GetReferenceProjectAccessAsync(
            ProjectAreaReferenceType,
            projectAreaId,
            cancellationToken);
        if (project is null)
        {
            return ServiceResult<MeasurementImageAreaLinkResponseDto>.NotFound(ProjectAreaNotFoundMessage);
        }

        if (!CanManageAreaFiles(project, currentUserId, roleName))
        {
            return ServiceResult<MeasurementImageAreaLinkResponseDto>.Forbidden(
                "You do not have access to manage measurement images for this project area.");
        }

        var areaLink = await _files.GetFileLinkEntityAsync(
            ProjectAreaReferenceType,
            projectAreaId,
            fileId,
            cancellationToken);
        if (areaLink is null || areaLink.FileType != FileType.SPACE_IMAGE)
        {
            return ServiceResult<MeasurementImageAreaLinkResponseDto>.Failure(
                Error.NotFound(
                    MeasurementImageErrorCodes.AreaLinkNotFound,
                    "Measurement image is not linked to this project area."));
        }

        await ExecuteInTransactionAsync(
            async ct =>
            {
                _files.RemoveFileLinks([areaLink]);
                await _unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);

        return ServiceResult<MeasurementImageAreaLinkResponseDto>.Success(
            new MeasurementImageAreaLinkResponseDto
            {
                ProjectAreaId = projectAreaId,
                FileId = fileId,
                FileLinkId = areaLink.FileLinkId
            },
            "Measurement image unlinked from project area successfully.");
    }

    private async Task<ServiceResult<T>?> ValidateGalleryAccessAsync<T>(
        Guid projectId,
        Guid currentUserId,
        MeasurementImageGalleryQueryDto query,
        CancellationToken cancellationToken)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<T>.Unauthorized();
        }

        var paginationErrors = ValidatePagination(query.Page, query.Limit);
        if (paginationErrors.Count > 0)
        {
            return ServiceResult<T>.BadRequest(paginationErrors);
        }

        var roleName = await _files.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return ServiceResult<T>.Forbidden(InactiveOrMissingRoleMessage);
        }

        var project = await _files.GetProjectAccessAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<T>.NotFound(ProjectNotFoundMessage);
        }

        if (!CanAccessProject(project, currentUserId, roleName))
        {
            return ServiceResult<T>.Forbidden("You do not have access to view measurement images for this project.");
        }

        return null;
    }

    private static MeasurementImageGalleryQueryReadModel BuildGalleryQuery(
        Guid projectId,
        MeasurementImageGalleryQueryDto query,
        string? roleName,
        Guid currentUserId)
    {
        return new MeasurementImageGalleryQueryReadModel
        {
            ProjectId = projectId,
            ScheduleId = query.ScheduleId,
            ProjectAreaId = query.ProjectAreaId,
            Assigned = query.Assigned,
            CustomerVisibleOnly = IsCustomer(roleName),
            CustomerAccountId = IsCustomer(roleName) ? currentUserId : null,
            Page = query.Page,
            Limit = query.Limit
        };
    }

    private static ServiceResult<MeasurementImageGalleryResponseDto> BuildGallerySuccess(
        MeasurementImageGalleryPageReadModel page,
        MeasurementImageGalleryQueryDto query,
        string message)
    {
        return ServiceResult<MeasurementImageGalleryResponseDto>.Success(
            new MeasurementImageGalleryResponseDto
            {
                Items = page.Items.Adapt<List<MeasurementImageGalleryItemDto>>(),
                Page = query.Page,
                Limit = query.Limit,
                Total = page.Total
            },
            message);
    }

    private static ServiceResult<ProjectFileUploadResponseDto>? ValidateCaptureEligibility(
        ProjectScheduleDetailReadModel schedule)
    {
        if (schedule.ScheduleType != ProjectScheduleType.MEASUREMENT)
        {
            return BadRequestUpload(
                MeasurementImageErrorCodes.ScheduleNotEligible,
                "Measurement images can only be registered for MEASUREMENT schedules.");
        }

        if (schedule.Status != ProjectScheduleStatus.CONFIRMED)
        {
            return BadRequestUpload(
                MeasurementImageErrorCodes.ScheduleNotEligible,
                "Measurement images can only be registered while the schedule is CONFIRMED.");
        }

        if (DateTime.UtcNow < schedule.ScheduledStart)
        {
            return BadRequestUpload(
                MeasurementImageErrorCodes.CaptureBeforeStart,
                "Measurement images cannot be registered before the scheduled start time.");
        }

        return null;
    }

    private List<string> ValidateRegisterRequest(RegisterMeasurementImageRequestDto request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.StoragePath))
        {
            errors.Add("Storage path is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PublicUrl))
        {
            errors.Add("Public URL is required.");
        }

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

        var extension = Path.GetExtension(request.OriginalFileName);
        if (string.IsNullOrWhiteSpace(extension) ||
            !AllowedImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("Only image file extensions are allowed for measurement photos.");
        }

        var contentType = ProjectFileUploadSupport.NormalizeContentType(request.ContentType);
        if (!AllowedImageMimeTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("Only image MIME types are allowed for measurement photos.");
        }

        return errors;
    }

    private bool TryParseProjectStoragePath(
        string storagePath,
        Guid projectId,
        out Guid fileId,
        out string generatedFileName)
    {
        fileId = Guid.Empty;
        generatedFileName = string.Empty;

        var normalizedPath = storagePath.Trim().Trim('/');
        var prefix = string.IsNullOrWhiteSpace(_firebaseSettings.ProjectFilesPrefix)
            ? "projects"
            : _firebaseSettings.ProjectFilesPrefix.Trim().Trim('/');
        var expectedPrefix = $"{prefix}/{projectId:D}/";

        if (!normalizedPath.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        generatedFileName = normalizedPath[expectedPrefix.Length..];
        if (generatedFileName.Length <= 32)
        {
            return false;
        }

        var idPart = generatedFileName[..32];
        if (!Guid.TryParseExact(idPart, "N", out fileId))
        {
            return false;
        }

        var extension = Path.GetExtension(generatedFileName);
        return !string.IsNullOrWhiteSpace(extension) &&
               AllowedImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private long ResolveMaxFileSize()
    {
        return _uploadSettings.MaxFileSizeBytes > 0
            ? _uploadSettings.MaxFileSizeBytes
            : _firebaseSettings.MaxFileSizeBytes;
    }

    private static bool CanCaptureMeasurementImage(
        ProjectScheduleDetailReadModel schedule,
        Guid currentUserId,
        string roleName)
    {
        if (IsAdmin(roleName))
        {
            return true;
        }

        return schedule.AssignedStaffId == currentUserId;
    }

    private static bool CanAccessProject(
        ProjectFileAccessReadModel project,
        Guid currentUserId,
        string roleName)
    {
        return roleName.ToUpperInvariant() switch
        {
            ApplicationRoles.Admin => true,
            ApplicationRoles.Customer => project.CustomerId == currentUserId,
            ApplicationRoles.Sales => project.AssignedSalesId == currentUserId,
            ApplicationRoles.Designer => project.AssignedDesignerId == currentUserId,
            _ => false
        };
    }

    private static bool CanManageAreaFiles(
        ProjectFileAccessReadModel project,
        Guid currentUserId,
        string roleName)
    {
        return roleName.ToUpperInvariant() switch
        {
            ApplicationRoles.Admin => true,
            ApplicationRoles.Sales => project.AssignedSalesId == currentUserId,
            ApplicationRoles.Designer => project.AssignedDesignerId == currentUserId,
            _ => false
        };
    }

    private static bool IsAdmin(string roleName)
    {
        return string.Equals(roleName, ApplicationRoles.Admin, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCustomer(string? roleName)
    {
        return string.Equals(roleName, ApplicationRoles.Customer, StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> ValidatePagination(int page, int limit)
    {
        var errors = new List<string>();
        if (page <= 0)
        {
            errors.Add("Page must be greater than zero.");
        }

        if (limit <= 0 || limit > 100)
        {
            errors.Add("Limit must be between 1 and 100.");
        }

        return errors;
    }

    private static ServiceResult<ProjectFileUploadResponseDto> BadRequestUpload(string errorCode, string message)
    {
        return ServiceResult<ProjectFileUploadResponseDto>.Failure(Error.BadRequest(errorCode, message));
    }

    private async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await action(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private Task SyncProjectFileIndexAsync(Guid fileId, CancellationToken cancellationToken)
    {
        return _projectFileSearchIndexer?.SyncFileAsync(fileId, cancellationToken) ?? Task.CompletedTask;
    }
}
