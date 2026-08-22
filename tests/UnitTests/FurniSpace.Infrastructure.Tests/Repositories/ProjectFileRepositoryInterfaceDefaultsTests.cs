#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class ProjectFileRepositoryInterfaceDefaultsTests
{
    [Fact]
    public async Task DefaultInterfaceMethods_ReturnExpectedFallbackValues()
    {
        IProjectFileRepository repository = new MinimalProjectFileRepository();

        Assert.Null(await repository.GetFileLinkEntityAsync("PROJECT", Guid.NewGuid(), Guid.NewGuid()));
        Assert.Empty(await repository.GetFileLinkEntitiesByReferenceAsync("PROJECT", Guid.NewGuid()));
        Assert.Null(await repository.GetProjectLinkedActiveFileAsync(Guid.NewGuid(), Guid.NewGuid()));
        Assert.False(await repository.ExistsByStoragePathAsync("path/to/file"));
        Assert.NotNull(await repository.GetMeasurementImageGalleryAsync(new MeasurementImageGalleryQueryReadModel()));
        Assert.False(await repository.HasMeasurementScheduleLinkInProjectAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    private sealed class MinimalProjectFileRepository : IProjectFileRepository
    {
        public Task<ProjectFileAccessReadModel?> GetProjectAccessAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProjectFileAccessReadModel?> GetReferenceProjectAccessAsync(
            string referenceType,
            Guid referenceId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<string?> GetAccountRoleNameAsync(
            Guid accountId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task AddFileLinkAsync(FileLink fileLink, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FileMetadataReadModel?> GetFileMetadataAsync(
            Guid fileId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FileReferencePageReadModel> GetFilesByReferenceAsync(
            FileReferenceQueryReadModel query,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FileLinkReadModel?> GetFileLinkAsync(
            Guid fileLinkId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<FileLink>> GetFileLinkEntitiesByFileIdAsync(
            Guid fileId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public void RemoveFileLinks(IEnumerable<FileLink> fileLinks) => throw new NotSupportedException();

        public Task<IReadOnlyList<CatalogFileReadModel>> GetCatalogFilesByReferencesAsync(
            string referenceType,
            IReadOnlyList<Guid> referenceIds,
            bool customerVisibleOnly,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> CountProductPreviewFilesAsync(Guid productId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ProductPreviewImageReadModel>> GetProductPreviewFilesAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProductPreviewImageReadModel?> GetProductPreviewFileAsync(
            Guid productId,
            Guid fileId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<FileLink>> GetProductPreviewFileLinkEntitiesAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> CountProductVersionPreviewFilesAsync(
            Guid productVersionId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<FileLink>> GetProductVersionPreviewFileLinkEntitiesAsync(
            Guid productVersionId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProjectFileSearchIndexItemReadModel?> GetSearchIndexItemAsync(
            Guid fileId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ProjectFileSearchIndexItemReadModel>> GetSearchIndexPageAsync(
            int page,
            int limit,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ProjectFileSearchIndexItemReadModel>> SearchByProjectAsync(
            Guid projectId,
            string query,
            int page,
            int limit,
            bool customerVisibleOnly,
            Guid? customerAccountId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> CountSearchByProjectAsync(
            Guid projectId,
            string query,
            bool customerVisibleOnly,
            Guid? customerAccountId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> HasProjectFileWithTypesAsync(
            Guid projectId,
            IReadOnlyCollection<FileType> fileTypes,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task AddAsync(StoredFile entity, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<StoredFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public void Update(StoredFile entity) => throw new NotSupportedException();

        public void Remove(StoredFile entity) => throw new NotSupportedException();

        public Task<IReadOnlyList<StoredFile>> ListAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task AddRangeAsync(IEnumerable<StoredFile> entities, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IQueryable<StoredFile> Query() => throw new NotSupportedException();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
