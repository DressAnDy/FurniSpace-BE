using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Storage;
using FurniSpace.Application.Constants.Common;
using static FurniSpace.Application.Constants.ProjectFiles.ProjectFileServiceConstants;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.DTOs.ProjectFiles;
using FurniSpace.Application.Interfaces.ProjectFiles;
using FurniSpace.Application.Interfaces.Search;
using FurniSpace.Application.Services.Search;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using FurniSpace.Infrastructure.Common.Search.Documents;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;

namespace FurniSpace.Application.Services.ProjectFiles;

public sealed class ProjectFileService : IProjectFileService
{
    private readonly IProjectFileRepository _projectFiles;
    private readonly IProductRepository _products;
    private readonly IProductVersionRepository _productVersions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _storage;
    private readonly FileUploadSettings _uploadSettings;
    private readonly FirebaseStorageSettings _firebaseSettings;
    private readonly ISearchIndexService? _search;
    private readonly IProjectFileSearchIndexer? _projectFileSearchIndexer;

    public ProjectFileService(
        IProjectFileRepository projectFiles,
        IProductRepository products,
        IProductVersionRepository productVersions,
        ProjectFileServiceDependencies dependencies)
    {
        _projectFiles = projectFiles;
        _products = products;
        _productVersions = productVersions;
        _unitOfWork = dependencies.UnitOfWork;
        _storage = dependencies.Storage;
        _uploadSettings = dependencies.UploadSettings;
        _firebaseSettings = dependencies.FirebaseSettings;
        _search = dependencies.Search;
        _projectFileSearchIndexer = dependencies.ProjectFileSearchIndexer;
    }

    public async Task<ServiceResult<ProjectFileUploadResponseDto>> UploadProjectFileAsync(
        Guid projectId,
        Guid currentUserId,
        UploadProjectFileRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            return ServiceResult<ProjectFileUploadResponseDto>.BadRequest("Project id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectFileUploadResponseDto>.Unauthorized("Authenticated account id is required.");
        }

        var validationErrors = ValidateRequest(request);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<ProjectFileUploadResponseDto>.BadRequest(validationErrors);
        }

        var project = await _projectFiles.GetProjectAccessAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectFileUploadResponseDto>.NotFound("Project not found.");
        }

        var roleName = await _projectFiles.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return ServiceResult<ProjectFileUploadResponseDto>.Forbidden(InactiveOrMissingRoleMessage);
        }

        if (!CanUpload(project.CustomerId, project.AssignedSalesId, project.AssignedDesignerId, currentUserId, roleName))
        {
            return ServiceResult<ProjectFileUploadResponseDto>.Forbidden("You do not have access to upload files to this project.");
        }

        return await UploadLinkedFileAsync(
            projectId,
            ProjectReferenceType,
            projectId,
            currentUserId,
            roleName,
            request,
            "Project file uploaded successfully.",
            cancellationToken);
    }

    public async Task<ServiceResult<ProjectFileUploadResponseDto>> UploadProjectAreaFileAsync(
        Guid projectAreaId,
        Guid currentUserId,
        UploadProjectFileRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (projectAreaId == Guid.Empty)
        {
            return ServiceResult<ProjectFileUploadResponseDto>.BadRequest("Project area id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectFileUploadResponseDto>.Unauthorized("Authenticated account id is required.");
        }

        var validationErrors = ValidateRequest(request);
        validationErrors.AddRange(ValidateProjectAreaFileRequest(request));
        if (validationErrors.Count > 0)
        {
            return ServiceResult<ProjectFileUploadResponseDto>.BadRequest(validationErrors);
        }

        var roleName = await _projectFiles.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return ServiceResult<ProjectFileUploadResponseDto>.Forbidden(InactiveOrMissingRoleMessage);
        }

        var project = await _projectFiles.GetReferenceProjectAccessAsync(
            ProjectAreaReferenceType,
            projectAreaId,
            cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectFileUploadResponseDto>.NotFound("Project area not found.");
        }

        if (!CanManageAreaFiles(project, currentUserId, roleName))
        {
            return ServiceResult<ProjectFileUploadResponseDto>.Forbidden("You do not have access to upload files to this project area.");
        }

        return await UploadLinkedFileAsync(
            project.ProjectId,
            ProjectAreaReferenceType,
            projectAreaId,
            currentUserId,
            roleName,
            request,
            "Project area file uploaded successfully.",
            cancellationToken);
    }

    public async Task<ServiceResult<FileDetailResponseDto>> GetFileDetailAsync(
        Guid fileId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (fileId == Guid.Empty)
        {
            return ServiceResult<FileDetailResponseDto>.BadRequest("File id is required.");
        }

        var roleName = await GetRequiredRoleNameAsync(currentUserId, cancellationToken);
        if (roleName is null)
        {
            return ServiceResult<FileDetailResponseDto>.Forbidden(InactiveOrMissingRoleMessage);
        }

        var file = await _projectFiles.GetFileMetadataAsync(fileId, cancellationToken);
        if (file is null)
        {
            return ServiceResult<FileDetailResponseDto>.NotFound(FileNotFoundMessage);
        }

        if (!IsAdmin(roleName) && file.Status != FileStatus.ACTIVE)
        {
            return ServiceResult<FileDetailResponseDto>.NotFound(FileNotFoundMessage);
        }

        if (!CanViewFile(file, currentUserId, roleName))
        {
            return ServiceResult<FileDetailResponseDto>.Forbidden("You do not have access to view this file.");
        }

        return ServiceResult<FileDetailResponseDto>.Success(
            file.Adapt<FileDetailResponseDto>(),
            "File detail retrieved successfully.");
    }

    public async Task<ServiceResult<ProjectFilesResponseDto>> GetProjectFilesAsync(
        Guid projectId,
        Guid currentUserId,
        ProjectFilesQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            return ServiceResult<ProjectFilesResponseDto>.BadRequest("Project id is required.");
        }

        var pageErrors = ValidatePagination(query.Page, query.Limit);
        if (pageErrors.Count > 0)
        {
            return ServiceResult<ProjectFilesResponseDto>.BadRequest(pageErrors);
        }

        var roleName = await GetRequiredRoleNameAsync(currentUserId, cancellationToken);
        if (roleName is null)
        {
            return ServiceResult<ProjectFilesResponseDto>.Forbidden(InactiveOrMissingRoleMessage);
        }

        var project = await _projectFiles.GetProjectAccessAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectFilesResponseDto>.NotFound("Project not found.");
        }

        if (!CanAccessProject(project, currentUserId, roleName))
        {
            return ServiceResult<ProjectFilesResponseDto>.Forbidden("You do not have access to view files for this project.");
        }

        var page = await _projectFiles.GetFilesByReferenceAsync(
            new FileReferenceQueryReadModel
            {
                ReferenceType = ProjectReferenceType,
                ReferenceId = projectId,
                FileType = query.FileType,
                Visibility = query.Visibility,
                CustomerVisibleOnly = IsCustomer(roleName),
                CustomerAccountId = IsCustomer(roleName) ? currentUserId : null,
                Page = query.Page,
                Limit = query.Limit
            },
            cancellationToken);

        return ServiceResult<ProjectFilesResponseDto>.Success(
            new ProjectFilesResponseDto
            {
                Items = page.Items.Adapt<List<FileListItemDto>>(),
                Page = query.Page,
                Limit = query.Limit,
                Total = page.Total
            },
            "Project files retrieved successfully.");
    }

    public async Task<ServiceResult<ProjectFilesResponseDto>> GetProjectAreaFilesAsync(
        Guid projectAreaId,
        Guid currentUserId,
        ProjectFilesQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (projectAreaId == Guid.Empty)
        {
            return ServiceResult<ProjectFilesResponseDto>.BadRequest("Project area id is required.");
        }

        var pageErrors = ValidatePagination(query.Page, query.Limit);
        if (pageErrors.Count > 0)
        {
            return ServiceResult<ProjectFilesResponseDto>.BadRequest(pageErrors);
        }

        var roleName = await GetRequiredRoleNameAsync(currentUserId, cancellationToken);
        if (roleName is null)
        {
            return ServiceResult<ProjectFilesResponseDto>.Forbidden(InactiveOrMissingRoleMessage);
        }

        var project = await _projectFiles.GetReferenceProjectAccessAsync(
            ProjectAreaReferenceType,
            projectAreaId,
            cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectFilesResponseDto>.NotFound("Project area not found.");
        }

        if (!CanAccessProject(project, currentUserId, roleName))
        {
            return ServiceResult<ProjectFilesResponseDto>.Forbidden("You do not have access to view files for this project area.");
        }

        var page = await _projectFiles.GetFilesByReferenceAsync(
            new FileReferenceQueryReadModel
            {
                ReferenceType = ProjectAreaReferenceType,
                ReferenceId = projectAreaId,
                FileType = query.FileType,
                Visibility = query.Visibility,
                CustomerVisibleOnly = IsCustomer(roleName),
                CustomerAccountId = IsCustomer(roleName) ? currentUserId : null,
                Page = query.Page,
                Limit = query.Limit
            },
            cancellationToken);

        return ServiceResult<ProjectFilesResponseDto>.Success(
            new ProjectFilesResponseDto
            {
                Items = page.Items.Adapt<List<FileListItemDto>>(),
                Page = query.Page,
                Limit = query.Limit,
                Total = page.Total
            },
            "Project area files retrieved successfully.");
    }

    public async Task<ServiceResult<ProjectAreaFilePrimaryResponseDto>> SetProjectAreaPrimaryFileAsync(
        Guid projectAreaId,
        Guid fileId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (projectAreaId == Guid.Empty || fileId == Guid.Empty)
        {
            return ServiceResult<ProjectAreaFilePrimaryResponseDto>.BadRequest("Project area id and file id are required.");
        }

        var roleName = await GetRequiredRoleNameAsync(currentUserId, cancellationToken);
        if (roleName is null)
        {
            return ServiceResult<ProjectAreaFilePrimaryResponseDto>.Forbidden(InactiveOrMissingRoleMessage);
        }

        var project = await _projectFiles.GetReferenceProjectAccessAsync(
            ProjectAreaReferenceType,
            projectAreaId,
            cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectAreaFilePrimaryResponseDto>.NotFound("Project area not found.");
        }

        if (!CanManageAreaFiles(project, currentUserId, roleName))
        {
            return ServiceResult<ProjectAreaFilePrimaryResponseDto>.Forbidden("You do not have access to update files for this project area.");
        }

        var selectedLink = await _projectFiles.GetFileLinkEntityAsync(
            ProjectAreaReferenceType,
            projectAreaId,
            fileId,
            cancellationToken);
        if (selectedLink is null)
        {
            return ServiceResult<ProjectAreaFilePrimaryResponseDto>.NotFound(FileNotFoundMessage);
        }

        if (!IsSupportedProjectAreaFileType(selectedLink.FileType))
        {
            return ServiceResult<ProjectAreaFilePrimaryResponseDto>.BadRequest("Only project area blueprint file types can be set as primary.");
        }

        var links = await _projectFiles.GetFileLinkEntitiesByReferenceAsync(
            ProjectAreaReferenceType,
            projectAreaId,
            cancellationToken);

        await ExecuteInTransactionAsync(
            async ct =>
            {
                foreach (var link in links.Where(link => IsSupportedProjectAreaFileType(link.FileType)))
                {
                    link.IsPrimary = link.FileId == fileId;
                }

                await _unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);

        return ServiceResult<ProjectAreaFilePrimaryResponseDto>.Success(
            new ProjectAreaFilePrimaryResponseDto
            {
                ProjectAreaId = projectAreaId,
                FileId = fileId,
                FileLinkId = selectedLink.FileLinkId,
                FileType = selectedLink.FileType,
                IsPrimary = true
            },
            "Primary project area file updated successfully.");
    }

    public async Task<ServiceResult<ProjectFileSearchResponseDto>> SearchProjectFilesAsync(
        Guid projectId,
        Guid currentUserId,
        string query,
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            return ServiceResult<ProjectFileSearchResponseDto>.BadRequest("Project id is required.");
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return ServiceResult<ProjectFileSearchResponseDto>.BadRequest("Search query is required.");
        }

        var pageErrors = ValidatePagination(page, limit);
        if (pageErrors.Count > 0)
        {
            return ServiceResult<ProjectFileSearchResponseDto>.BadRequest(pageErrors);
        }

        var roleName = await GetRequiredRoleNameAsync(currentUserId, cancellationToken);
        if (roleName is null)
        {
            return ServiceResult<ProjectFileSearchResponseDto>.Forbidden(InactiveOrMissingRoleMessage);
        }

        var project = await _projectFiles.GetProjectAccessAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectFileSearchResponseDto>.NotFound("Project not found.");
        }

        if (!CanAccessProject(project, currentUserId, roleName))
        {
            return ServiceResult<ProjectFileSearchResponseDto>.Forbidden("You do not have access to search files for this project.");
        }

        var customerVisibleOnly = IsCustomer(roleName);
        Guid? customerAccountId = customerVisibleOnly ? currentUserId : null;
        var response = await SearchProjectFilesWithFallbackAsync(
            projectId,
            query,
            page,
            limit,
            customerVisibleOnly,
            customerAccountId,
            cancellationToken);

        return ServiceResult<ProjectFileSearchResponseDto>.Success(
            response,
            "Project files search completed successfully.");
    }

    public async Task<ServiceResult<FilesByReferenceResponseDto>> GetFilesByReferenceAsync(
        Guid currentUserId,
        FilesByReferenceQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var normalizedReferenceType = NormalizeReferenceType(query.ReferenceType);
        var validationErrors = ValidateByReferenceQuery(normalizedReferenceType, query.ReferenceId, query.Page, query.Limit);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<FilesByReferenceResponseDto>.BadRequest(validationErrors);
        }

        if (CatalogReferenceTypes.Contains(normalizedReferenceType))
        {
            return await GetCatalogFilesByReferenceAsync(currentUserId, normalizedReferenceType, query, cancellationToken);
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<FilesByReferenceResponseDto>.Unauthorized();
        }

        var roleName = await GetRequiredRoleNameAsync(currentUserId, cancellationToken);
        if (roleName is null)
        {
            return ServiceResult<FilesByReferenceResponseDto>.Forbidden(InactiveOrMissingRoleMessage);
        }

        var project = await _projectFiles.GetReferenceProjectAccessAsync(normalizedReferenceType, query.ReferenceId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<FilesByReferenceResponseDto>.NotFound("Referenced object not found.");
        }

        if (!CanAccessProject(project, currentUserId, roleName))
        {
            return ServiceResult<FilesByReferenceResponseDto>.Forbidden("You do not have access to view files for this reference.");
        }

        var page = await _projectFiles.GetFilesByReferenceAsync(
            new FileReferenceQueryReadModel
            {
                ReferenceType = normalizedReferenceType,
                ReferenceId = query.ReferenceId,
                FileType = query.FileType,
                Visibility = query.Visibility,
                CustomerVisibleOnly = IsCustomer(roleName),
                CustomerAccountId = IsCustomer(roleName) ? currentUserId : null,
                Page = query.Page,
                Limit = query.Limit
            },
            cancellationToken);

        return ServiceResult<FilesByReferenceResponseDto>.Success(
            new FilesByReferenceResponseDto
            {
                ReferenceType = normalizedReferenceType,
                ReferenceId = query.ReferenceId,
                Items = page.Items.Adapt<List<FileListItemDto>>(),
                Page = query.Page,
                Limit = query.Limit,
                Total = page.Total
            },
            "Files retrieved successfully.");
    }

    private async Task<ServiceResult<FilesByReferenceResponseDto>> GetCatalogFilesByReferenceAsync(
        Guid currentUserId,
        string normalizedReferenceType,
        FilesByReferenceQueryDto query,
        CancellationToken cancellationToken)
    {
        var referenceExists = normalizedReferenceType == CatalogFileReferenceTypes.Product
            ? await _products.GetByIdAsync(query.ReferenceId, cancellationToken) is not null
            : await _productVersions.GetByIdAsync(query.ReferenceId, cancellationToken) is not null;

        if (!referenceExists)
        {
            return ServiceResult<FilesByReferenceResponseDto>.NotFound("Referenced object not found.");
        }

        string? roleName = null;
        if (currentUserId != Guid.Empty)
        {
            roleName = await _projectFiles.GetAccountRoleNameAsync(currentUserId, cancellationToken);
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return ServiceResult<FilesByReferenceResponseDto>.Forbidden(InactiveOrMissingRoleMessage);
            }
        }

        var customerVisibleOnly = roleName is null || !IsAdmin(roleName);

        var page = await _projectFiles.GetFilesByReferenceAsync(
            new FileReferenceQueryReadModel
            {
                ReferenceType = normalizedReferenceType,
                ReferenceId = query.ReferenceId,
                FileType = query.FileType,
                Visibility = customerVisibleOnly ? null : query.Visibility,
                CustomerVisibleOnly = customerVisibleOnly,
                CustomerAccountId = null,
                Page = query.Page,
                Limit = query.Limit
            },
            cancellationToken);

        return ServiceResult<FilesByReferenceResponseDto>.Success(
            new FilesByReferenceResponseDto
            {
                ReferenceType = normalizedReferenceType,
                ReferenceId = query.ReferenceId,
                Items = page.Items.Adapt<List<FileListItemDto>>(),
                Page = query.Page,
                Limit = query.Limit,
                Total = page.Total
            },
            "Files retrieved successfully.");
    }

    public async Task<ServiceResult<DeleteFileResponseDto>> DeleteFileAsync(
        Guid fileId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (fileId == Guid.Empty)
        {
            return ServiceResult<DeleteFileResponseDto>.BadRequest("File id is required.");
        }

        var roleName = await GetRequiredRoleNameAsync(currentUserId, cancellationToken);
        if (roleName is null)
        {
            return ServiceResult<DeleteFileResponseDto>.Forbidden(InactiveOrMissingRoleMessage);
        }

        var metadata = await _projectFiles.GetFileMetadataAsync(fileId, cancellationToken);
        if (metadata is null)
        {
            return ServiceResult<DeleteFileResponseDto>.NotFound(FileNotFoundMessage);
        }

        if (!CanDeleteFile(metadata, currentUserId, roleName))
        {
            return ServiceResult<DeleteFileResponseDto>.Forbidden("You do not have access to delete this file.");
        }

        var file = await _projectFiles.GetByIdAsync(fileId, cancellationToken);
        if (file is null)
        {
            return ServiceResult<DeleteFileResponseDto>.NotFound(FileNotFoundMessage);
        }

        await _storage.DeleteAsync(file.StoragePath, cancellationToken);

        var fileLinks = await _projectFiles.GetFileLinkEntitiesByFileIdAsync(fileId, cancellationToken);
        await ExecuteInTransactionAsync(
            async ct =>
            {
                _projectFiles.RemoveFileLinks(fileLinks);
                _projectFiles.Remove(file);
                await _unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);

        return ServiceResult<DeleteFileResponseDto>.Success(
            new DeleteFileResponseDto
            {
                FileId = fileId,
                DeletedAt = DateTime.UtcNow
            },
            "File deleted successfully.");
    }

    public async Task<ServiceResult<ArchiveFileResponseDto>> ArchiveFileAsync(
        Guid fileId,
        Guid currentUserId,
        ArchiveFileRequestDto request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        if (fileId == Guid.Empty)
        {
            return ServiceResult<ArchiveFileResponseDto>.BadRequest("File id is required.");
        }

        var roleName = await GetRequiredRoleNameAsync(currentUserId, cancellationToken);
        if (roleName is null)
        {
            return ServiceResult<ArchiveFileResponseDto>.Forbidden(InactiveOrMissingRoleMessage);
        }

        var metadata = await _projectFiles.GetFileMetadataAsync(fileId, cancellationToken);
        if (metadata is null)
        {
            return ServiceResult<ArchiveFileResponseDto>.NotFound(FileNotFoundMessage);
        }

        if (metadata.Status != FileStatus.ACTIVE)
        {
            return ServiceResult<ArchiveFileResponseDto>.Conflict("Only active files can be archived.");
        }

        if (!CanArchiveFile(metadata, currentUserId, roleName))
        {
            return ServiceResult<ArchiveFileResponseDto>.Forbidden("You do not have access to archive this file.");
        }

        var file = await _projectFiles.GetByIdAsync(fileId, cancellationToken);
        if (file is null)
        {
            return ServiceResult<ArchiveFileResponseDto>.NotFound(FileNotFoundMessage);
        }

        var archivedAt = DateTime.UtcNow;
        await ExecuteInTransactionAsync(
            async ct =>
            {
                file.Status = FileStatus.ARCHIVED;
                file.ArchivedAt = archivedAt;
                _projectFiles.Update(file);
                await _unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);

        return ServiceResult<ArchiveFileResponseDto>.Success(
            new ArchiveFileResponseDto
            {
                FileId = fileId,
                Status = FileStatus.ARCHIVED,
                ArchivedAt = archivedAt
            },
            "File archived successfully.");
    }

    private List<string> ValidateRequest(UploadProjectFileRequestDto request)
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

        var maxFileSize = ResolveMaxFileSize();
        if (request.FileSizeBytes > maxFileSize)
        {
            errors.Add($"File size must not exceed {maxFileSize} bytes.");
        }

        var extension = Path.GetExtension(request.OriginalFileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions().Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("File extension is not allowed.");
        }

        var contentType = NormalizeContentType(request.ContentType);
        if (!AllowedMimeTypes().Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("File MIME type is not allowed.");
        }

        return errors;
    }

    private static List<string> ValidateProjectAreaFileRequest(UploadProjectFileRequestDto request)
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

    private async Task<ServiceResult<ProjectFileUploadResponseDto>> UploadLinkedFileAsync(
        Guid projectId,
        string referenceType,
        Guid referenceId,
        Guid currentUserId,
        string roleName,
        UploadProjectFileRequestDto request,
        string successMessage,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var fileId = Guid.NewGuid();
        var fileLinkId = Guid.NewGuid();
        var originalFileName = Path.GetFileName(request.OriginalFileName.Trim());
        var generatedFileName = BuildGeneratedFileName(fileId, originalFileName);
        var objectName = BuildProjectObjectName(projectId, generatedFileName);
        var visibility = ResolveVisibility(request.Visibility, roleName);

        var uploadResult = await _storage.UploadAsync(
            new StorageUploadRequest
            {
                Content = request.Content,
                ObjectName = objectName,
                ContentType = NormalizeContentType(request.ContentType)
            },
            cancellationToken);

        var storedFile = CreateStoredFile(
            fileId,
            currentUserId,
            originalFileName,
            generatedFileName,
            request,
            uploadResult,
            now);
        var fileLink = CreateFileLink(
            fileLinkId,
            fileId,
            referenceType,
            referenceId,
            currentUserId,
            request,
            visibility,
            now);

        try
        {
            await ExecuteInTransactionAsync(
                async ct =>
                {
                    await _projectFiles.AddAsync(storedFile, ct);
                    await _projectFiles.AddFileLinkAsync(fileLink, ct);
                    if (fileLink.IsPrimary == true)
                    {
                        await ClearOtherPrimaryProjectAreaLinksAsync(fileLink, ct);
                    }

                    await _unitOfWork.SaveChangesAsync(ct);
                },
                cancellationToken);
        }
        catch
        {
            await _storage.DeleteAsync(uploadResult.ObjectName, cancellationToken);
            throw;
        }

        await SyncProjectFileIndexAsync(fileId, cancellationToken);

        return ServiceResult<ProjectFileUploadResponseDto>.Created(
            BuildUploadResponse(projectId, referenceType, referenceId, storedFile, fileLink, uploadResult),
            successMessage);
    }

    private async Task ClearOtherPrimaryProjectAreaLinksAsync(
        FileLink primaryLink,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(primaryLink.ReferenceType, ProjectAreaReferenceType, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var links = await _projectFiles.GetFileLinkEntitiesByReferenceAsync(
            ProjectAreaReferenceType,
            primaryLink.ReferenceId,
            cancellationToken);
        foreach (var link in links.Where(link =>
                     link.FileLinkId != primaryLink.FileLinkId &&
                     IsSupportedProjectAreaFileType(link.FileType)))
        {
            link.IsPrimary = false;
        }
    }

    private static StoredFile CreateStoredFile(
        Guid fileId,
        Guid currentUserId,
        string originalFileName,
        string generatedFileName,
        UploadProjectFileRequestDto request,
        StorageUploadResult uploadResult,
        DateTime now) =>
        new()
        {
            FileId = fileId,
            UploadedBy = currentUserId,
            OriginalFileName = originalFileName,
            StoredFileName = generatedFileName,
            FileUrl = uploadResult.PublicUrl,
            StoragePath = uploadResult.ObjectName,
            MimeType = NormalizeContentType(request.ContentType),
            FileExtension = NormalizeExtension(originalFileName),
            FileSizeBytes = request.FileSizeBytes,
            Status = FileStatus.ACTIVE,
            UploadedAt = now
        };

    private static FileLink CreateFileLink(
        Guid fileLinkId,
        Guid fileId,
        string referenceType,
        Guid referenceId,
        Guid currentUserId,
        UploadProjectFileRequestDto request,
        FileVisibility visibility,
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

    private static ProjectFileUploadResponseDto BuildUploadResponse(
        Guid projectId,
        string referenceType,
        Guid referenceId,
        StoredFile storedFile,
        FileLink fileLink,
        StorageUploadResult uploadResult) =>
        new()
        {
            FileId = storedFile.FileId,
            FileLinkId = fileLink.FileLinkId,
            ProjectId = projectId,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            OriginalFileName = storedFile.OriginalFileName,
            FileName = storedFile.StoredFileName,
            FileType = fileLink.FileType ?? FileType.OTHER,
            MimeType = storedFile.MimeType,
            FileSize = storedFile.FileSizeBytes,
            StoragePath = storedFile.StoragePath,
            PublicUrl = uploadResult.PublicUrl,
            Visibility = fileLink.Visibility ?? FileVisibility.PRIVATE,
            IsPrimary = fileLink.IsPrimary == true,
            DisplayOrder = fileLink.DisplayOrder,
            UploadedBy = storedFile.UploadedBy,
            UploadedAt = storedFile.UploadedAt
        };

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

    private static List<string> ValidateByReferenceQuery(
        string referenceType,
        Guid referenceId,
        int page,
        int limit)
    {
        var errors = ValidatePagination(page, limit);
        if (string.IsNullOrWhiteSpace(referenceType))
        {
            errors.Add("Reference type is required.");
        }
        else if (!SupportedReferenceTypes.Contains(referenceType))
        {
            errors.Add("Reference type is not supported.");
        }

        if (referenceId == Guid.Empty)
        {
            errors.Add("Reference id is required.");
        }

        return errors;
    }

    private async Task<string?> GetRequiredRoleNameAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (currentUserId == Guid.Empty)
        {
            return null;
        }

        return await _projectFiles.GetAccountRoleNameAsync(currentUserId, cancellationToken);
    }

    private long ResolveMaxFileSize()
    {
        return _uploadSettings.MaxFileSizeBytes > 0
            ? _uploadSettings.MaxFileSizeBytes
            : _firebaseSettings.MaxFileSizeBytes;
    }

    private string[] AllowedExtensions()
    {
        return _uploadSettings.AllowedExtensions.Length == 0
            ? new FileUploadSettings().AllowedExtensions
            : _uploadSettings.AllowedExtensions;
    }

    private string[] AllowedMimeTypes()
    {
        return _uploadSettings.AllowedMimeTypes.Length == 0
            ? new FileUploadSettings().AllowedMimeTypes
            : _uploadSettings.AllowedMimeTypes;
    }

    private static bool CanUpload(
        Guid customerId,
        Guid? assignedSalesId,
        Guid? assignedDesignerId,
        Guid currentUserId,
        string roleName)
    {
        return roleName.ToUpperInvariant() switch
        {
            ApplicationRoles.Admin => true,
            ApplicationRoles.Customer => customerId == currentUserId,
            ApplicationRoles.Sales => assignedSalesId == currentUserId,
            ApplicationRoles.Designer => assignedDesignerId == currentUserId,
            _ => false
        };
    }

    private static bool CanAccessProject(
        ProjectFileAccessReadModel project,
        Guid currentUserId,
        string roleName)
    {
        return CanUpload(
            project.CustomerId,
            project.AssignedSalesId,
            project.AssignedDesignerId,
            currentUserId,
            roleName);
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

    private static bool CanViewFile(
        FileMetadataReadModel file,
        Guid currentUserId,
        string roleName)
    {
        if (IsAdmin(roleName) || file.UploadedBy == currentUserId)
        {
            return true;
        }

        if (file.ProjectAccess is null || !CanAccessProject(file.ProjectAccess, currentUserId, roleName))
        {
            return false;
        }

        return !IsCustomer(roleName) || file.Visibility == FileVisibility.CUSTOMER_VISIBLE;
    }

    private static bool CanDeleteFile(
        FileMetadataReadModel file,
        Guid currentUserId,
        string roleName)
    {
        if (IsAdmin(roleName))
        {
            return true;
        }

        if (file.UploadedBy != currentUserId)
        {
            return false;
        }

        if (file.ProjectAccess is null)
        {
            return true;
        }

        if (IsCustomer(roleName))
        {
            return file.ProjectAccess.CustomerId == currentUserId &&
                IsBeforeProposalSelected(file.ProjectAccess.Status);
        }

        return IsBeforeOrderConfirmed(file.ProjectAccess.Status);
    }

    private static bool CanArchiveFile(
        FileMetadataReadModel file,
        Guid currentUserId,
        string roleName)
    {
        if (IsAdmin(roleName))
        {
            return true;
        }

        if (file.UploadedBy != currentUserId)
        {
            return false;
        }

        return file.ProjectAccess is null || IsBeforeOrderConfirmed(file.ProjectAccess.Status);
    }

    private static bool IsAdmin(string roleName)
    {
        return string.Equals(roleName, ApplicationRoles.Admin, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCustomer(string roleName)
    {
        return string.Equals(roleName, ApplicationRoles.Customer, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBeforeProposalSelected(ProjectStatus? status)
    {
        return ProjectStatusRank(status) < ProjectStatusRank(ProjectStatus.PROPOSAL_SELECTED);
    }

    private static bool IsBeforeOrderConfirmed(ProjectStatus? status)
    {
        return ProjectStatusRank(status) < ProjectStatusRank(ProjectStatus.ORDER_CONFIRMED);
    }

    private static int ProjectStatusRank(ProjectStatus? status)
    {
        if (!status.HasValue)
        {
            return 0;
        }

        return ProjectStatusRanks.TryGetValue(status.Value, out var rank) ? rank : 200;
    }

    private static FileVisibility ResolveVisibility(FileVisibility? requestedVisibility, string roleName)
    {
        if (requestedVisibility.HasValue)
        {
            return requestedVisibility.Value;
        }

        return string.Equals(roleName, ApplicationRoles.Customer, StringComparison.OrdinalIgnoreCase)
            ? FileVisibility.CUSTOMER_VISIBLE
            : FileVisibility.STAFF_ONLY;
    }

    private static bool IsSupportedProjectAreaFileType(FileType? fileType)
    {
        return fileType is FileType.FLOOR_PLAN
            or FileType.PDF_DRAWING
            or FileType.REFERENCE_IMAGE
            or FileType.LIDAR_SCAN
            or FileType.MEASUREMENT_REPORT;
    }

    private string BuildProjectObjectName(Guid projectId, string generatedFileName)
    {
        var prefix = string.IsNullOrWhiteSpace(_firebaseSettings.ProjectFilesPrefix)
            ? "projects"
            : _firebaseSettings.ProjectFilesPrefix.Trim().Trim('/');

        return $"{prefix}/{projectId:D}/{generatedFileName}";
    }

    private static string BuildGeneratedFileName(Guid fileId, string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        return $"{fileId:N}{extension}";
    }

    private static string? NormalizeExtension(string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        return extension.TrimStart('.').ToLowerInvariant();
    }

    private static string NormalizeContentType(string? contentType)
    {
        return string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeReferenceType(string? referenceType)
    {
        return string.IsNullOrWhiteSpace(referenceType)
            ? string.Empty
            : referenceType.Trim().ToUpperInvariant();
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

    private async Task<ProjectFileSearchResponseDto> GetProjectFilesSearchFromRepositoryAsync(
        Guid projectId,
        string query,
        int page,
        int limit,
        bool customerVisibleOnly,
        Guid? customerAccountId,
        CancellationToken cancellationToken)
    {
        var items = await _projectFiles.SearchByProjectAsync(
            projectId,
            query,
            page,
            limit,
            customerVisibleOnly,
            customerAccountId,
            cancellationToken);
        var total = await _projectFiles.CountSearchByProjectAsync(
            projectId,
            query,
            customerVisibleOnly,
            customerAccountId,
            cancellationToken);

        return new ProjectFileSearchResponseDto
        {
            Items = items
                .Select(item => new ProjectFileSearchItemDto
                {
                    FileId = item.FileId,
                    ProjectId = item.ProjectId,
                    ReferenceType = item.ReferenceType,
                    ReferenceId = item.ReferenceId,
                    OriginalFileName = item.OriginalFileName,
                    FileType = item.FileType?.ToString(),
                    Visibility = item.Visibility?.ToString(),
                    MimeType = item.MimeType,
                    UploadedAt = item.UploadedAt
                })
                .ToList(),
            Page = page,
            Limit = limit,
            Total = total
        };
    }

    private async Task<ProjectFileSearchResponseDto> SearchProjectFilesWithFallbackAsync(
        Guid projectId,
        string query,
        int page,
        int limit,
        bool customerVisibleOnly,
        Guid? customerAccountId,
        CancellationToken cancellationToken)
    {
        if (_search is null)
        {
            return await GetProjectFilesSearchFromRepositoryAsync(
                projectId,
                query,
                page,
                limit,
                customerVisibleOnly,
                customerAccountId,
                cancellationToken);
        }

        try
        {
            var searchResult = await _search.SearchAsync<ProjectFileSearchDocument>(
                ProjectFileIndexName,
                ProjectFileElasticsearchQueryFactory.BuildProjectSearch(
                    projectId,
                    query,
                    page,
                    limit,
                    customerVisibleOnly,
                    customerAccountId),
                cancellationToken);

            return new ProjectFileSearchResponseDto
            {
                Items = searchResult.Documents.Select(ProjectFileSearchResponseMapper.ToItem).ToList(),
                Page = page,
                Limit = limit,
                Total = (int)Math.Min(searchResult.Total, int.MaxValue)
            };
        }
        catch
        {
            return await GetProjectFilesSearchFromRepositoryAsync(
                projectId,
                query,
                page,
                limit,
                customerVisibleOnly,
                customerAccountId,
                cancellationToken);
        }
    }

    private Task SyncProjectFileIndexAsync(Guid fileId, CancellationToken cancellationToken)
    {
        return _projectFileSearchIndexer?.SyncFileAsync(fileId, cancellationToken) ?? Task.CompletedTask;
    }
}
