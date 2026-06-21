#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.DTOs.Products;
using FurniSpace.Infrastructure.DTOs.ProjectFiles;
using FurniSpace.Infrastructure.Repositories.IRepository;

namespace FurniSpace.Application.Tests.TestDoubles;

public sealed class FakeCatalogProjectFileRepository : IProjectFileRepository
{
    public IReadOnlyList<CatalogFileReadModel> CatalogFiles { get; init; } = [];

    public Task<IReadOnlyList<CatalogFileReadModel>> GetCatalogFilesByReferencesAsync(
        string referenceType,
        IReadOnlyList<Guid> referenceIds,
        bool customerVisibleOnly,
        CancellationToken cancellationToken = default)
    {
        _ = customerVisibleOnly;
        var normalizedReferenceType = referenceType.Trim().ToUpperInvariant();
        var idSet = referenceIds.ToHashSet();
        var files = CatalogFiles
            .Where(file =>
                string.Equals(file.ReferenceType, normalizedReferenceType, StringComparison.Ordinal) &&
                idSet.Contains(file.ReferenceId))
            .ToList();

        return Task.FromResult<IReadOnlyList<CatalogFileReadModel>>(files);
    }

    public Task<ProjectFileAccessReadModel?> GetProjectAccessAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Task.FromResult<ProjectFileAccessReadModel?>(null);

    public Task<ProjectFileAccessReadModel?> GetReferenceProjectAccessAsync(
        string referenceType,
        Guid referenceId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<ProjectFileAccessReadModel?>(null);

    public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    public Task AddFileLinkAsync(FileLink fileLink, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<FileMetadataReadModel?> GetFileMetadataAsync(Guid fileId, CancellationToken cancellationToken = default)
        => Task.FromResult<FileMetadataReadModel?>(null);

    public Task<FileReferencePageReadModel> GetFilesByReferenceAsync(
        FileReferenceQueryReadModel query,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new FileReferencePageReadModel());

    public Task<FileLinkReadModel?> GetFileLinkAsync(Guid fileLinkId, CancellationToken cancellationToken = default)
        => Task.FromResult<FileLinkReadModel?>(null);

    public Task<IReadOnlyList<FileLink>> GetFileLinkEntitiesByFileIdAsync(
        Guid fileId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<FileLink>>([]);

    public void RemoveFileLinks(IEnumerable<FileLink> fileLinks)
    {
    }

    public IQueryable<StoredFile> Query() => Enumerable.Empty<StoredFile>().AsQueryable();

    public Task<StoredFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult<StoredFile?>(null);

    public Task<IReadOnlyList<StoredFile>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<StoredFile>>([]);

    public Task AddAsync(StoredFile entity, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task AddRangeAsync(IEnumerable<StoredFile> entities, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public void Update(StoredFile entity)
    {
    }

    public void Remove(StoredFile entity)
    {
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(1);

    public Task<int> CountProductPreviewFilesAsync(Guid productId, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<IReadOnlyList<ProductPreviewImageReadModel>> GetProductPreviewFilesAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ProductPreviewImageReadModel>>([]);

    public Task<ProductPreviewImageReadModel?> GetProductPreviewFileAsync(
        Guid productId,
        Guid fileId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<ProductPreviewImageReadModel?>(null);

    public Task<IReadOnlyList<FileLink>> GetProductPreviewFileLinkEntitiesAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<FileLink>>([]);
}
