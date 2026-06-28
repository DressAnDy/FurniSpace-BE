using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Storage;
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
    private const string ProjectFileIndexName = "project-files";
    private const string AdminRole = "ADMIN";
    private const string CustomerRole = "CUSTOMER";
    private const string SalesRole = "SALES";
    private const string DesignerRole = "DESIGNER";
    private const string ProjectReferenceType = "PROJECT";
    private const string InactiveOrMissingRoleMessage = "Authenticated account is not active or has no role.";
    private const string FileNotFoundMessage = "File not found.";
    private static readonly HashSet<string> SupportedReferenceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ProjectReferenceType,
        "PROJECT_SCHEDULE",
        "PROPOSAL",
        "QUOTATION",
        "ORDER",
        CatalogFileReferenceTypes.Product,
        CatalogFileReferenceTypes.ProductVersion
    };
    private static readonly HashSet<string> CatalogReferenceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        CatalogFileReferenceTypes.Product,
        CatalogFileReferenceTypes.ProductVersion
    };
    private static readonly Dictionary<ProjectStatus, int> ProjectStatusRanks = new()
    {
        [ProjectStatus.SUBMITTED] = 10,
        [ProjectStatus.IN_CONSULTATION] = 20,
        [ProjectStatus.NEED_BASIC_INFORMATION] = 30,
        [ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT] = 40,
        [ProjectStatus.MEASUREMENT_REQUIRED] = 50,
        [ProjectStatus.SPACE_VERIFIED] = 60,
        [ProjectStatus.PROPOSAL_DRAFTING] = 70,
        [ProjectStatus.WAITING_FOR_CUSTOMER_REVIEW] = 80,
        [ProjectStatus.REVISION_REQUESTED] = 90,
        [ProjectStatus.PROPOSAL_SELECTED] = 100,
        [ProjectStatus.QUOTATION_SENT] = 110,
        [ProjectStatus.QUOTATION_REVISION_REQUESTED] = 120,
        [ProjectStatus.ORDER_CONFIRMED] = 130,
        [ProjectStatus.IN_PRODUCTION] = 140,
        [ProjectStatus.PRODUCTION_BLOCKED] = 150,
        [ProjectStatus.READY_FOR_DELIVERY] = 160,
        [ProjectStatus.DELIVERING] = 170,
        [ProjectStatus.DELIVERED] = 180,
        [ProjectStatus.COMPLETED] = 190,
        [ProjectStatus.REJECTED] = 200
    };

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

        var storedFile = new StoredFile
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

        var fileLink = new FileLink
        {
            FileLinkId = fileLinkId,
            FileId = fileId,
            ReferenceType = ProjectReferenceType,
            ReferenceId = projectId,
            FileType = request.FileType,
            Visibility = visibility,
            Description = NormalizeOptional(request.Note),
            CreatedBy = currentUserId,
            CreatedAt = now
        };

        try
        {
            await ExecuteInTransactionAsync(
                async ct =>
                {
                    await _projectFiles.AddAsync(storedFile, ct);
                    await _projectFiles.AddFileLinkAsync(fileLink, ct);
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

        var response = new ProjectFileUploadResponseDto
        {
            FileId = fileId,
            FileLinkId = fileLinkId,
            ProjectId = projectId,
            OriginalFileName = originalFileName,
            FileName = storedFile.StoredFileName,
            FileType = request.FileType,
            MimeType = storedFile.MimeType,
            FileSize = storedFile.FileSizeBytes,
            StoragePath = storedFile.StoragePath,
            PublicUrl = uploadResult.PublicUrl,
            Visibility = visibility,
            UploadedBy = currentUserId,
            UploadedAt = storedFile.UploadedAt
        };

        return ServiceResult<ProjectFileUploadResponseDto>.Created(
            response,
            "Project file uploaded successfully.");
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
        ProjectFileSearchResponseDto response;
        if (_search is not null)
        {
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
                        IsCustomer(roleName) ? currentUserId : null),
                    cancellationToken);

                response = new ProjectFileSearchResponseDto
                {
                    Items = searchResult.Documents.Select(ProjectFileSearchResponseMapper.ToItem).ToList(),
                    Page = page,
                    Limit = limit,
                    Total = (int)Math.Min(searchResult.Total, int.MaxValue)
                };
            }
            catch
            {
                response = await GetProjectFilesSearchFromRepositoryAsync(
                    projectId,
                    query,
                    page,
                    limit,
                    customerVisibleOnly,
                    IsCustomer(roleName) ? currentUserId : null,
                    cancellationToken);
            }
        }
        else
        {
            response = await GetProjectFilesSearchFromRepositoryAsync(
                projectId,
                query,
                page,
                limit,
                customerVisibleOnly,
                IsCustomer(roleName) ? currentUserId : null,
                cancellationToken);
        }

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
            AdminRole => true,
            CustomerRole => customerId == currentUserId,
            SalesRole => assignedSalesId == currentUserId,
            DesignerRole => assignedDesignerId == currentUserId,
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
        return string.Equals(roleName, AdminRole, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCustomer(string roleName)
    {
        return string.Equals(roleName, CustomerRole, StringComparison.OrdinalIgnoreCase);
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

        return string.Equals(roleName, CustomerRole, StringComparison.OrdinalIgnoreCase)
            ? FileVisibility.CUSTOMER_VISIBLE
            : FileVisibility.STAFF_ONLY;
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

    private Task SyncProjectFileIndexAsync(Guid fileId, CancellationToken cancellationToken)
    {
        return _projectFileSearchIndexer?.SyncFileAsync(fileId, cancellationToken) ?? Task.CompletedTask;
    }
}
