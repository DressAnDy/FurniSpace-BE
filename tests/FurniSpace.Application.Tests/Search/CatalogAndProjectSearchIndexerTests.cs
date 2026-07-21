#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Services.Search;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Search;
using FurniSpace.Infrastructure.Common.Search.Documents;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Search;

public sealed class CatalogAndProjectSearchIndexerTests
{
    [Fact]
    public async Task SyncProductAsync_WithIndexableProduct_IndexesDocument()
    {
        var productId = Guid.NewGuid();
        var search = new CapturingSearchIndexService();
        var indexer = new ProductSearchIndexer(
            new FakeProductRepository(new ProductListItemReadModel
            {
                ProductId = productId,
                ProductName = "Sofa",
                Status = ProductStatus.ACTIVE,
                DefaultVersion = new ProductVersionReadModel
                {
                    ProductVersionId = Guid.NewGuid(),
                    VersionCode = "SOFA-1",
                    VersionName = "Sofa Default",
                    Status = ProductStatus.ACTIVE,
                    IsPublic = true,
                    CreatedAt = new DateTime(2026, 6, 28, 0, 0, 0, DateTimeKind.Utc)
                }
            }),
            search);

        await indexer.SyncProductAsync(productId);

        Assert.Equal("products", search.IndexName);
        Assert.Equal(productId.ToString(), search.IndexId);
        Assert.IsType<ProductSearchDocument>(search.IndexedDocument);
        Assert.Equal(0, search.DeleteCallCount);
    }

    [Fact]
    public async Task SyncProductAsync_WithMissingOrNonIndexableProduct_DeletesDocument()
    {
        var productId = Guid.NewGuid();
        var search = new CapturingSearchIndexService();
        var missingIndexer = new ProductSearchIndexer(new FakeProductRepository(null), search);

        await missingIndexer.SyncProductAsync(productId);

        Assert.Equal("products", search.DeleteIndexName);
        Assert.Equal(productId.ToString(), search.DeleteId);

        var inactiveSearch = new CapturingSearchIndexService();
        var inactiveIndexer = new ProductSearchIndexer(
            new FakeProductRepository(new ProductListItemReadModel
            {
                ProductId = productId,
                ProductName = "Hidden sofa",
                Status = ProductStatus.INACTIVE
            }),
            inactiveSearch);

        await inactiveIndexer.SyncProductAsync(productId);

        Assert.Equal(1, inactiveSearch.DeleteCallCount);
        Assert.Equal(0, inactiveSearch.IndexCallCount);
    }

    [Fact]
    public async Task SyncFileAsync_WithIndexableFile_IndexesDocument()
    {
        var fileId = Guid.NewGuid();
        var search = new CapturingSearchIndexService();
        var indexer = new ProjectFileSearchIndexer(
            new FakeProjectFileRepository(new ProjectFileSearchIndexItemReadModel
            {
                FileId = fileId,
                ProjectId = Guid.NewGuid(),
                ReferenceType = "PROJECT",
                ReferenceId = Guid.NewGuid(),
                OriginalFileName = "measurement.pdf",
                Status = FileStatus.ACTIVE
            }),
            search);

        await indexer.SyncFileAsync(fileId);

        Assert.Equal("project-files", search.IndexName);
        Assert.Equal(fileId.ToString(), search.IndexId);
        Assert.IsType<ProjectFileSearchDocument>(search.IndexedDocument);
        Assert.Equal(0, search.DeleteCallCount);
    }

    [Fact]
    public async Task SyncFileAsync_WithMissingOrBlankFile_DeletesDocument()
    {
        var fileId = Guid.NewGuid();
        var search = new CapturingSearchIndexService();
        var missingIndexer = new ProjectFileSearchIndexer(new FakeProjectFileRepository(null), search);

        await missingIndexer.SyncFileAsync(fileId);

        Assert.Equal("project-files", search.DeleteIndexName);
        Assert.Equal(fileId.ToString(), search.DeleteId);

        var blankSearch = new CapturingSearchIndexService();
        var blankIndexer = new ProjectFileSearchIndexer(
            new FakeProjectFileRepository(new ProjectFileSearchIndexItemReadModel
            {
                FileId = fileId,
                OriginalFileName = " "
            }),
            blankSearch);

        await blankIndexer.SyncFileAsync(fileId);

        Assert.Equal(1, blankSearch.DeleteCallCount);
        Assert.Equal(0, blankSearch.IndexCallCount);
    }

    [Fact]
    public async Task SyncProjectAsync_WithProject_IndexesDocument()
    {
        var projectId = Guid.NewGuid();
        var search = new CapturingSearchIndexService();
        var indexer = new ProjectSearchIndexer(
            new FakeProjectRepository(new ProjectSearchIndexItemReadModel
            {
                ProjectId = projectId,
                ProjectName = "Kitchen Renovation",
                CustomerId = Guid.NewGuid(),
                CustomerName = "Customer",
                CustomerEmail = "customer@example.com"
            }),
            search);

        await indexer.SyncProjectAsync(projectId);

        Assert.Equal("projects", search.IndexName);
        Assert.Equal(projectId.ToString(), search.IndexId);
        Assert.IsType<ProjectSearchDocument>(search.IndexedDocument);
        Assert.Equal(0, search.DeleteCallCount);
    }

    [Fact]
    public async Task SyncProjectAsync_WithMissingProject_DeletesDocument()
    {
        var projectId = Guid.NewGuid();
        var search = new CapturingSearchIndexService();
        var indexer = new ProjectSearchIndexer(new FakeProjectRepository(null), search);

        await indexer.SyncProjectAsync(projectId);

        Assert.Equal("projects", search.DeleteIndexName);
        Assert.Equal(projectId.ToString(), search.DeleteId);
        Assert.Equal(0, search.IndexCallCount);
    }

    [Fact]
    public async Task SyncIndexers_WhenRepositoryOrSearchThrows_DoNotBubble()
    {
        var throwingSearch = new CapturingSearchIndexService(throwOnIndex: true, throwOnDelete: true);
        var completedSyncCount = 0;

        await new ProductSearchIndexer(new FakeProductRepository(null, throwOnGet: true), throwingSearch)
            .SyncProductAsync(Guid.NewGuid());
        completedSyncCount++;
        await new ProjectFileSearchIndexer(new FakeProjectFileRepository(null, throwOnGet: true), throwingSearch)
            .SyncFileAsync(Guid.NewGuid());
        completedSyncCount++;
        await new ProjectSearchIndexer(new FakeProjectRepository(null, throwOnGet: true), throwingSearch)
            .SyncProjectAsync(Guid.NewGuid());
        completedSyncCount++;

        Assert.Equal(3, completedSyncCount);
        Assert.Equal(0, throwingSearch.IndexCallCount);
        Assert.Equal(0, throwingSearch.DeleteCallCount);
    }

    private sealed class CapturingSearchIndexService : ISearchIndexService
    {
        private readonly bool _throwOnIndex;
        private readonly bool _throwOnDelete;

        public CapturingSearchIndexService(bool throwOnIndex = false, bool throwOnDelete = false)
        {
            _throwOnIndex = throwOnIndex;
            _throwOnDelete = throwOnDelete;
        }

        public int IndexCallCount { get; private set; }
        public int DeleteCallCount { get; private set; }
        public string? IndexName { get; private set; }
        public string? IndexId { get; private set; }
        public object? IndexedDocument { get; private set; }
        public string? DeleteIndexName { get; private set; }
        public string? DeleteId { get; private set; }

        public Task IndexAsync<TDocument>(string indexName, string id, TDocument document, CancellationToken cancellationToken = default)
        {
            if (_throwOnIndex)
            {
                throw new InvalidOperationException("Index unavailable.");
            }

            IndexCallCount++;
            IndexName = indexName;
            IndexId = id;
            IndexedDocument = document;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string indexName, string id, CancellationToken cancellationToken = default)
        {
            if (_throwOnDelete)
            {
                throw new InvalidOperationException("Delete unavailable.");
            }

            DeleteCallCount++;
            DeleteIndexName = indexName;
            DeleteId = id;
            return Task.CompletedTask;
        }

        public Task BulkIndexAsync<TDocument>(string indexName, IReadOnlyList<BulkIndexItem<TDocument>> items, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task<SearchResult<TDocument>> SearchAsync<TDocument>(string indexName, SearchRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new SearchResult<TDocument>());
        public Task<IReadOnlyList<TDocument>> SearchAsync<TDocument>(string indexName, string query, int size = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TDocument>>([]);
        public Task<SuggestResult> SuggestAsync(string indexName, SuggestRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new SuggestResult());
        public Task<SearchResult<TDocument>> MoreLikeThisAsync<TDocument>(
            string indexName,
            string documentId,
            MoreLikeThisRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new SearchResult<TDocument>());
        public Task<SearchAggregationResult> AggregateAsync(
            string indexName,
            SearchAggregationRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new SearchAggregationResult());
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        private readonly ProductListItemReadModel? _item;
        private readonly bool _throwOnGet;

        public FakeProductRepository(ProductListItemReadModel? item, bool throwOnGet = false)
        {
            _item = item;
            _throwOnGet = throwOnGet;
        }

        public Task<ProductListItemReadModel?> GetSearchIndexItemAsync(Guid productId, CancellationToken cancellationToken = default)
        {
            if (_throwOnGet)
            {
                throw new InvalidOperationException("Repository unavailable.");
            }

            return Task.FromResult(_item?.ProductId == productId ? _item : null);
        }

        public Task<IReadOnlyList<ProductListItemReadModel>> GetSearchIndexPageAsync(int page, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductListItemReadModel>>([]);
        public Task<bool> ProductCodeExistsAsync(string productCode, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<ProductDetailReadModel?> GetDetailAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<ProductDetailReadModel?>(null);
        public Task<IReadOnlyList<ProductListItemReadModel>> GetPublicListAsync(int page, int limit, IReadOnlyCollection<int>? businessTypeIds = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductListItemReadModel>>([]);
        public Task<int> CountAsync(IReadOnlyCollection<int>? businessTypeIds = null, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<ProductCategoryReadModel?> GetCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default) => Task.FromResult<ProductCategoryReadModel?>(null);
        public Task<IReadOnlyList<ProductListItemReadModel>> GetPublicListByCategoryAsync(Guid categoryId, int page, int limit, bool includeDefaultVersion, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductListItemReadModel>>([]);
        public Task<int> CountByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<ProductSearchResultReadModel> SearchPublicAsync(ProductSearchQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult(new ProductSearchResultReadModel());
        public Task<IReadOnlyList<ProductListItemReadModel>> SuggestPublicAsync(string query, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductListItemReadModel>>([]);
        public Task<IReadOnlyList<ProductListItemReadModel>> GetSimilarPublicAsync(Guid productId, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductListItemReadModel>>([]);
        public IQueryable<Product> Query() => Enumerable.Empty<Product>().AsQueryable();
        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(null);
        public Task<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Product>>([]);
        public Task AddAsync(Product entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<Product> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(Product entity) { }
        public void Remove(Product entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class FakeProjectFileRepository : IProjectFileRepository
    {
        private readonly ProjectFileSearchIndexItemReadModel? _item;
        private readonly bool _throwOnGet;

        public FakeProjectFileRepository(ProjectFileSearchIndexItemReadModel? item, bool throwOnGet = false)
        {
            _item = item;
            _throwOnGet = throwOnGet;
        }

        public Task<ProjectFileSearchIndexItemReadModel?> GetSearchIndexItemAsync(Guid fileId, CancellationToken cancellationToken = default)
        {
            if (_throwOnGet)
            {
                throw new InvalidOperationException("Repository unavailable.");
            }

            return Task.FromResult(_item?.FileId == fileId ? _item : null);
        }

        public Task<IReadOnlyList<ProjectFileSearchIndexItemReadModel>> GetSearchIndexPageAsync(int page, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectFileSearchIndexItemReadModel>>([]);
        public Task<IReadOnlyList<ProjectFileSearchIndexItemReadModel>> SearchByProjectAsync(Guid projectId, string query, int page, int limit, bool customerVisibleOnly, Guid? customerAccountId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectFileSearchIndexItemReadModel>>([]);
        public Task<int> CountSearchByProjectAsync(Guid projectId, string query, bool customerVisibleOnly, Guid? customerAccountId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<ProjectFileAccessReadModel?> GetProjectAccessAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectFileAccessReadModel?>(null);
        public Task<ProjectFileAccessReadModel?> GetReferenceProjectAccessAsync(string referenceType, Guid referenceId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectFileAccessReadModel?>(null);
        public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task AddFileLinkAsync(FileLink fileLink, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<FileMetadataReadModel?> GetFileMetadataAsync(Guid fileId, CancellationToken cancellationToken = default) => Task.FromResult<FileMetadataReadModel?>(null);
        public Task<FileReferencePageReadModel> GetFilesByReferenceAsync(FileReferenceQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult(new FileReferencePageReadModel());
        public Task<FileLinkReadModel?> GetFileLinkAsync(Guid fileLinkId, CancellationToken cancellationToken = default) => Task.FromResult<FileLinkReadModel?>(null);
        public Task<IReadOnlyList<FileLink>> GetFileLinkEntitiesByFileIdAsync(Guid fileId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FileLink>>([]);
        public void RemoveFileLinks(IEnumerable<FileLink> fileLinks) { }
        public Task<IReadOnlyList<CatalogFileReadModel>> GetCatalogFilesByReferencesAsync(string referenceType, IReadOnlyList<Guid> referenceIds, bool customerVisibleOnly, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CatalogFileReadModel>>([]);
        public Task<int> CountProductPreviewFilesAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<ProductPreviewImageReadModel>> GetProductPreviewFilesAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductPreviewImageReadModel>>([]);
        public Task<ProductPreviewImageReadModel?> GetProductPreviewFileAsync(Guid productId, Guid fileId, CancellationToken cancellationToken = default) => Task.FromResult<ProductPreviewImageReadModel?>(null);
        public Task<IReadOnlyList<FileLink>> GetProductPreviewFileLinkEntitiesAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FileLink>>([]);
        public Task<int> CountProductVersionPreviewFilesAsync(Guid productVersionId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<FileLink>> GetProductVersionPreviewFileLinkEntitiesAsync(Guid productVersionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FileLink>>([]);
        public Task<bool> HasProjectFileWithTypesAsync(Guid projectId, IReadOnlyCollection<FileType> fileTypes, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public IQueryable<StoredFile> Query() => Enumerable.Empty<StoredFile>().AsQueryable();
        public Task<StoredFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<StoredFile?>(null);
        public Task<IReadOnlyList<StoredFile>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StoredFile>>([]);
        public Task AddAsync(StoredFile entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<StoredFile> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(StoredFile entity) { }
        public void Remove(StoredFile entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class FakeProjectRepository : IProjectRepository
    {
        private readonly ProjectSearchIndexItemReadModel? _item;
        private readonly bool _throwOnGet;

        public FakeProjectRepository(ProjectSearchIndexItemReadModel? item, bool throwOnGet = false)
        {
            _item = item;
            _throwOnGet = throwOnGet;
        }

        public Task<ProjectSearchIndexItemReadModel?> GetSearchIndexItemAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            if (_throwOnGet)
            {
                throw new InvalidOperationException("Repository unavailable.");
            }

            return Task.FromResult(_item?.ProjectId == projectId ? _item : null);
        }

        public Task<IReadOnlyList<ProjectSearchIndexItemReadModel>> GetSearchIndexPageAsync(int page, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectSearchIndexItemReadModel>>([]);
        public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<string?> GetAccountFullNameAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<IReadOnlyList<Guid>> GetActiveAccountIdsByRoleNamesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<int> CountSubmittedInYearAsync(int year, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<ProjectDetailReadModel?> GetDetailAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectDetailReadModel?>(null);
        public Task<DesignerAccountReadModel?> GetActiveDesignerAsync(Guid designerId, CancellationToken cancellationToken = default) => Task.FromResult<DesignerAccountReadModel?>(null);
        public Task<IReadOnlyList<ProjectListItemReadModel>> GetListAsync(ProjectListQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectListItemReadModel>>([]);
        public Task<int> CountAsync(ProjectListQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<ProjectByUserItemReadModel>> GetByUserAsync(ProjectByUserQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectByUserItemReadModel>>([]);
        public Task<int> CountByUserAsync(ProjectByUserQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public IQueryable<Project> Query() => Enumerable.Empty<Project>().AsQueryable();
        public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Project?>(null);
        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Project>>([]);
        public Task AddAsync(Project entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<Project> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(Project entity) { }
        public void Remove(Project entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }
}
