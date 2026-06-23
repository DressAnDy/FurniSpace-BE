using System.Linq.Expressions;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.DTOs.Products;
using FurniSpace.Infrastructure.DTOs.ProjectFiles;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class ProjectFileRepository : GenericRepository<StoredFile>, IProjectFileRepository
{
    private const string ProductReferenceType = "PRODUCT";
    private const string ProductVersionReferenceType = "PRODUCT_VERSION";

    private readonly Dictionary<string, Func<Guid, CancellationToken, Task<ProjectFileAccessReadModel?>>> _projectAccessResolvers;

    public ProjectFileRepository(AppDbContext dbContext) : base(dbContext)
    {
        _projectAccessResolvers = new Dictionary<string, Func<Guid, CancellationToken, Task<ProjectFileAccessReadModel?>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["PROJECT"] = GetProjectAccessAsync,
            ["PROJECT_SCHEDULE"] = GetProjectScheduleAccessAsync,
            ["PROPOSAL"] = GetProposalAccessAsync,
            ["QUOTATION"] = GetQuotationAccessAsync,
            ["ORDER"] = GetOrderAccessAsync
        };
    }

    public Task<ProjectFileAccessReadModel?> GetProjectAccessAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProjectSet
            .Where(project => project.ProjectId == projectId)
            .Select(project => new ProjectFileAccessReadModel
            {
                ProjectId = project.ProjectId,
                CustomerId = project.CustomerId,
                AssignedSalesId = project.AssignedSalesId,
                AssignedDesignerId = project.AssignedDesignerId,
                Status = project.Status
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<ProjectFileAccessReadModel?> GetReferenceProjectAccessAsync(
        string referenceType,
        Guid referenceId,
        CancellationToken cancellationToken = default)
    {
        var normalizedReferenceType = referenceType.Trim().ToUpperInvariant();

        return _projectAccessResolvers.TryGetValue(normalizedReferenceType, out var resolver)
            ? resolver(referenceId, cancellationToken)
            : Task.FromResult<ProjectFileAccessReadModel?>(null);
    }

    public Task<string?> GetAccountRoleNameAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.AccountSet
            .Where(account => account.AccountId == accountId && account.DeletedAt == null)
            .Join(
                DbContext.RoleSet,
                account => account.RoleId,
                role => role.RoleId,
                (_, role) => role.RoleName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddFileLinkAsync(
        FileLink fileLink,
        CancellationToken cancellationToken = default)
    {
        await DbContext.FileLinkSet.AddAsync(fileLink, cancellationToken);
    }

    public Task<FileMetadataReadModel?> GetFileMetadataAsync(
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        return BuildFileMetadataQuery()
            .Where(file => file.FileId == fileId)
            .OrderBy(file => file.FileLinkId == null)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<FileReferencePageReadModel> GetFilesByReferenceAsync(
        FileReferenceQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        var normalizedReferenceType = query.ReferenceType.Trim().ToUpperInvariant();
        var page = Math.Max(query.Page, 1);
        var limit = Math.Max(query.Limit, 1);

        var files = BuildLinkedFileMetadataQuery()
            .Where(file =>
                file.ReferenceType == normalizedReferenceType &&
                file.ReferenceId == query.ReferenceId &&
                file.Status == FileStatus.ACTIVE);

        if (query.FileType.HasValue)
        {
            files = files.Where(file => file.FileType == query.FileType);
        }

        if (query.Visibility.HasValue)
        {
            files = files.Where(file => file.Visibility == query.Visibility);
        }

        if (query.CustomerVisibleOnly && query.CustomerAccountId.HasValue)
        {
            var accountId = query.CustomerAccountId.Value;
            files = files.Where(file =>
                file.Visibility == FileVisibility.CUSTOMER_VISIBLE ||
                file.UploadedBy == accountId);
        }

        var total = await files.CountAsync(cancellationToken);
        var items = await files
            .OrderByDescending(file => file.FileType == FileType.PRODUCT_PREVIEW)
            .ThenBy(file => file.FileType == FileType.PRODUCT_PREVIEW
                ? file.DisplayOrder == null || file.DisplayOrder <= 0
                    ? int.MaxValue
                    : file.DisplayOrder
                : int.MaxValue)
            .ThenByDescending(file => file.UploadedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new FileReferencePageReadModel
        {
            Items = items,
            Total = total
        };
    }

    public Task<FileLinkReadModel?> GetFileLinkAsync(
        Guid fileLinkId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.FileLinkSet
            .Where(link => link.FileLinkId == fileLinkId)
            .Join(
                DbContext.StoredFileSet,
                link => link.FileId,
                file => file.FileId,
                (link, file) => new { link, file })
            .GroupJoin(
                DbContext.ProjectSet,
                joined => new
                {
                    ReferenceType = joined.link.ReferenceType,
                    ReferenceId = joined.link.ReferenceId
                },
                project => new
                {
                    ReferenceType = "PROJECT",
                    ReferenceId = project.ProjectId
                },
                (joined, projects) => new { joined.link, joined.file, projects })
            .SelectMany(
                joined => joined.projects.DefaultIfEmpty(),
                (joined, project) => new FileLinkReadModel
                {
                    FileLinkId = joined.link.FileLinkId,
                    FileId = joined.link.FileId,
                    ReferenceType = joined.link.ReferenceType,
                    ReferenceId = joined.link.ReferenceId,
                    FileType = joined.link.FileType,
                    Visibility = joined.link.Visibility,
                    CreatedBy = joined.link.CreatedBy,
                    UploadedBy = joined.file.UploadedBy,
                    ProjectAccess = project == null
                        ? null
                        : new ProjectFileAccessReadModel
                        {
                            ProjectId = project.ProjectId,
                            CustomerId = project.CustomerId,
                            AssignedSalesId = project.AssignedSalesId,
                            AssignedDesignerId = project.AssignedDesignerId,
                            Status = project.Status
                        }
                })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FileLink>> GetFileLinkEntitiesByFileIdAsync(
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        return await DbContext.FileLinkSet
            .Where(link => link.FileId == fileId)
            .ToListAsync(cancellationToken);
    }

    public void RemoveFileLinks(IEnumerable<FileLink> fileLinks)
    {
        DbContext.FileLinkSet.RemoveRange(fileLinks);
    }

    public async Task<IReadOnlyList<CatalogFileReadModel>> GetCatalogFilesByReferencesAsync(
        string referenceType,
        IReadOnlyList<Guid> referenceIds,
        bool customerVisibleOnly,
        CancellationToken cancellationToken = default)
    {
        if (referenceIds.Count == 0)
        {
            return [];
        }

        var normalizedReferenceType = referenceType.Trim().ToUpperInvariant();
        var idSet = referenceIds.Distinct().ToList();

        var query = DbContext.StoredFileSet
            .Join(
                DbContext.FileLinkSet,
                file => file.FileId,
                link => link.FileId,
                (file, link) => new { file, link })
            .Where(joined =>
                joined.link.ReferenceType == normalizedReferenceType &&
                idSet.Contains(joined.link.ReferenceId) &&
                joined.file.Status == FileStatus.ACTIVE);

        if (customerVisibleOnly)
        {
            query = query.Where(joined => joined.link.Visibility == FileVisibility.CUSTOMER_VISIBLE);
        }

        return await query
            .OrderByDescending(joined => joined.link.FileType == FileType.PRODUCT_PREVIEW)
            .ThenBy(joined => joined.link.FileType == FileType.PRODUCT_PREVIEW
                ? joined.link.DisplayOrder == null || joined.link.DisplayOrder <= 0
                    ? int.MaxValue
                    : joined.link.DisplayOrder
                : int.MaxValue)
            .ThenByDescending(joined => joined.file.UploadedAt)
            .Select(joined => new CatalogFileReadModel
            {
                FileId = joined.file.FileId,
                FileLinkId = joined.link.FileLinkId,
                ReferenceId = joined.link.ReferenceId,
                ReferenceType = joined.link.ReferenceType,
                FileType = joined.link.FileType,
                OriginalFileName = joined.file.OriginalFileName,
                FileUrl = joined.file.FileUrl,
                MimeType = joined.file.MimeType,
                FileSizeBytes = joined.file.FileSizeBytes,
                Visibility = joined.link.Visibility,
                Status = joined.file.Status,
                DisplayOrder = joined.link.DisplayOrder,
                IsPrimary = joined.link.IsPrimary,
                Description = joined.link.Description,
                UploadedAt = joined.file.UploadedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountProductPreviewFilesAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        return await CountReferencePreviewFilesAsync(ProductReferenceType, productId, cancellationToken);
    }

    public async Task<IReadOnlyList<ProductPreviewImageReadModel>> GetProductPreviewFilesAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        return await QueryActivePreviewFiles(ProductReferenceType, productId, customerVisibleOnly: true)
            .OrderBy(joined => joined.Link.DisplayOrder ?? int.MaxValue)
            .ThenBy(joined => joined.File.UploadedAt)
            .Select(ProductPreviewImageSelector)
            .ToListAsync(cancellationToken);
    }

    public Task<ProductPreviewImageReadModel?> GetProductPreviewFileAsync(
        Guid productId,
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        return QueryActivePreviewFiles(ProductReferenceType, productId, customerVisibleOnly: true)
            .Where(joined => joined.File.FileId == fileId)
            .Select(ProductPreviewImageSelector)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FileLink>> GetProductPreviewFileLinkEntitiesAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        return await GetReferencePreviewFileLinkEntitiesAsync(ProductReferenceType, productId, cancellationToken);
    }

    public Task<int> CountProductVersionPreviewFilesAsync(
        Guid productVersionId,
        CancellationToken cancellationToken = default)
    {
        return CountReferencePreviewFilesAsync(ProductVersionReferenceType, productVersionId, cancellationToken);
    }

    public async Task<IReadOnlyList<FileLink>> GetProductVersionPreviewFileLinkEntitiesAsync(
        Guid productVersionId,
        CancellationToken cancellationToken = default)
    {
        return await GetReferencePreviewFileLinkEntitiesAsync(ProductVersionReferenceType, productVersionId, cancellationToken);
    }

    private async Task<int> CountReferencePreviewFilesAsync(
        string referenceType,
        Guid referenceId,
        CancellationToken cancellationToken = default)
    {
        return await QueryActivePreviewFiles(referenceType, referenceId, customerVisibleOnly: false)
            .CountAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<FileLink>> GetReferencePreviewFileLinkEntitiesAsync(
        string referenceType,
        Guid referenceId,
        CancellationToken cancellationToken = default)
    {
        return await QueryActivePreviewFiles(referenceType, referenceId, customerVisibleOnly: false)
            .OrderBy(joined => joined.Link.DisplayOrder ?? int.MaxValue)
            .ThenBy(joined => joined.File.UploadedAt)
            .Select(joined => joined.Link)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<ProductPreviewJoin> QueryActivePreviewFiles(
        string referenceType,
        Guid referenceId,
        bool customerVisibleOnly)
    {
        var query = DbContext.StoredFileSet
            .Join(
                DbContext.FileLinkSet,
                file => file.FileId,
                link => link.FileId,
                (file, link) => new ProductPreviewJoin { File = file, Link = link })
            .Where(joined =>
                joined.Link.ReferenceType == referenceType &&
                joined.Link.ReferenceId == referenceId &&
                joined.Link.FileType == FileType.PRODUCT_PREVIEW &&
                joined.File.Status != FileStatus.ARCHIVED);

        if (customerVisibleOnly)
        {
            query = query.Where(joined => joined.Link.Visibility == FileVisibility.CUSTOMER_VISIBLE);
        }

        return query;
    }

    private static readonly Expression<Func<ProductPreviewJoin, ProductPreviewImageReadModel>> ProductPreviewImageSelector =
        joined => new ProductPreviewImageReadModel
        {
            FileId = joined.File.FileId,
            FileLinkId = joined.Link.FileLinkId,
            ProductId = joined.Link.ReferenceId,
            FileType = joined.Link.FileType ?? FileType.PRODUCT_PREVIEW,
            FileUrl = joined.File.FileUrl,
            MimeType = joined.File.MimeType,
            FileSizeBytes = joined.File.FileSizeBytes,
            DisplayOrder = joined.Link.DisplayOrder ?? 0,
            Description = joined.Link.Description,
            CreatedAt = joined.Link.CreatedAt ?? joined.File.UploadedAt,
            StoragePath = joined.File.StoragePath
        };

    private sealed class ProductPreviewJoin
    {
        public required StoredFile File { get; init; }

        public required FileLink Link { get; init; }
    }

    private IQueryable<FileMetadataReadModel> BuildFileMetadataQuery()
    {
        return BuildLinkedFileMetadataQuery()
            .Concat(BuildUnlinkedFileMetadataQuery());
    }

    private IQueryable<FileMetadataReadModel> BuildLinkedFileMetadataQuery()
    {
        return DbContext.StoredFileSet
            .Join(
                DbContext.FileLinkSet,
                file => file.FileId,
                link => link.FileId,
                (file, link) => new { file, link })
            .GroupJoin(
                DbContext.ProjectSet,
                joined => new
                {
                    joined.link.ReferenceType,
                    joined.link.ReferenceId
                },
                project => new
                {
                    ReferenceType = "PROJECT",
                    ReferenceId = project.ProjectId
                },
                (joined, projects) => new { joined.file, joined.link, projects })
            .SelectMany(
                joined => joined.projects.DefaultIfEmpty(),
                (joined, project) => new FileMetadataReadModel
                {
                    FileId = joined.file.FileId,
                    FileLinkId = joined.link.FileLinkId,
                    ReferenceType = joined.link.ReferenceType,
                    ReferenceId = joined.link.ReferenceId,
                    OriginalFileName = joined.file.OriginalFileName,
                    StoredFileName = joined.file.StoredFileName,
                    FileType = joined.link.FileType,
                    MimeType = joined.file.MimeType,
                    FileSizeBytes = joined.file.FileSizeBytes,
                    StoragePath = joined.file.StoragePath,
                    FileUrl = joined.file.FileUrl,
                    Visibility = joined.link.Visibility,
                    UploadedBy = joined.file.UploadedBy,
                    UploadedAt = joined.file.UploadedAt,
                    Status = joined.file.Status,
                    DisplayOrder = joined.link.DisplayOrder,
                    IsPrimary = joined.link.IsPrimary,
                    ProjectAccess = project == null
                        ? null
                        : new ProjectFileAccessReadModel
                        {
                            ProjectId = project.ProjectId,
                            CustomerId = project.CustomerId,
                            AssignedSalesId = project.AssignedSalesId,
                            AssignedDesignerId = project.AssignedDesignerId,
                            Status = project.Status
                        }
                });
    }

    private IQueryable<FileMetadataReadModel> BuildUnlinkedFileMetadataQuery()
    {
        return DbContext.StoredFileSet
            .Where(file => !DbContext.FileLinkSet.Any(link => link.FileId == file.FileId))
            .Select(file => new FileMetadataReadModel
            {
                FileId = file.FileId,
                OriginalFileName = file.OriginalFileName,
                StoredFileName = file.StoredFileName,
                MimeType = file.MimeType,
                FileSizeBytes = file.FileSizeBytes,
                StoragePath = file.StoragePath,
                FileUrl = file.FileUrl,
                UploadedBy = file.UploadedBy,
                UploadedAt = file.UploadedAt,
                Status = file.Status
            });
    }

    private Task<ProjectFileAccessReadModel?> GetProjectScheduleAccessAsync(
        Guid referenceId,
        CancellationToken cancellationToken)
    {
        return ProjectAccessByProjectIdAsync(
            DbContext.ProjectScheduleSet
                .Where(schedule => schedule.ScheduleId == referenceId)
                .Select(schedule => schedule.ProjectId),
            cancellationToken);
    }

    private Task<ProjectFileAccessReadModel?> GetProposalAccessAsync(
        Guid referenceId,
        CancellationToken cancellationToken)
    {
        return ProjectAccessByProjectIdAsync(
            DbContext.ProposalSet
                .Where(proposal => proposal.ProposalId == referenceId)
                .Select(proposal => proposal.ProjectId),
            cancellationToken);
    }

    private Task<ProjectFileAccessReadModel?> GetQuotationAccessAsync(
        Guid referenceId,
        CancellationToken cancellationToken)
    {
        return ProjectAccessByProjectIdAsync(
            DbContext.QuotationSet
                .Where(quotation => quotation.QuotationId == referenceId)
                .Select(quotation => quotation.ProjectId),
            cancellationToken);
    }

    private Task<ProjectFileAccessReadModel?> GetOrderAccessAsync(
        Guid referenceId,
        CancellationToken cancellationToken)
    {
        return ProjectAccessByProjectIdAsync(
            DbContext.OrderSet
                .Where(order => order.OrderId == referenceId)
                .Select(order => order.ProjectId),
            cancellationToken);
    }

    private Task<ProjectFileAccessReadModel?> ProjectAccessByProjectIdAsync(
        IQueryable<Guid> projectIds,
        CancellationToken cancellationToken)
    {
        return DbContext.ProjectSet
            .Where(project => projectIds.Contains(project.ProjectId))
            .Select(project => new ProjectFileAccessReadModel
            {
                ProjectId = project.ProjectId,
                CustomerId = project.CustomerId,
                AssignedSalesId = project.AssignedSalesId,
                AssignedDesignerId = project.AssignedDesignerId,
                Status = project.Status
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
