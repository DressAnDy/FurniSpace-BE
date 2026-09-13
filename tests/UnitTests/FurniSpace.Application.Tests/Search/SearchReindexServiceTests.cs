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
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.ReadModels.Accounts;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.ReadModels.ProjectChatMessages;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Search;

public sealed class SearchReindexServiceTests
{
    [Fact]
    public async Task ReindexAllAsync_EnsuresIndicesIndexesValidItemsAndDeletesNonIndexableItems()
    {
        var productId = Guid.NewGuid();
        var hiddenProductId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var blankMessageId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var blankFileId = Guid.NewGuid();
        var search = new CapturingSearchIndexService();
        var indexManager = new CapturingIndexManager();
        var service = new SearchReindexService(
            new FakeAccountRepository(CreateAccounts(101)),
            new FakeProductRepository(
            [
                new ProductListItemReadModel
                {
                    ProductId = productId,
                    ProductName = "Public sofa",
                    Status = ProductStatus.ACTIVE,
                    DefaultVersion = new ProductVersionReadModel
                    {
                        ProductVersionId = Guid.NewGuid(),
                        VersionCode = "SOFA-1",
                        VersionName = "Default",
                        Status = ProductStatus.ACTIVE,
                        IsPublic = true,
                        CreatedAt = DateTime.UtcNow
                    }
                },
                new ProductListItemReadModel
                {
                    ProductId = hiddenProductId,
                    ProductName = "Hidden sofa",
                    Status = ProductStatus.INACTIVE
                }
            ]),
            new FakeProjectRepository(
            [
                new ProjectSearchIndexItemReadModel
                {
                    ProjectId = Guid.NewGuid(),
                    ProjectName = "Kitchen",
                    CustomerId = Guid.NewGuid(),
                    CustomerName = "Customer",
                    CustomerEmail = "customer@example.com"
                }
            ]),
            new FakeChatMessageRepository(
            [
                new ChatMessageSearchIndexItemReadModel
                {
                    MessageId = messageId,
                    ChatId = Guid.NewGuid(),
                    ProjectId = Guid.NewGuid(),
                    SenderId = Guid.NewGuid(),
                    SenderName = "Designer",
                    MessageType = ProjectChatMessageType.TEXT,
                    Content = "Hello",
                    CreatedAt = DateTime.UtcNow
                },
                new ChatMessageSearchIndexItemReadModel
                {
                    MessageId = blankMessageId,
                    Content = " "
                }
            ]),
            new FakeProjectFileRepository(
            [
                new ProjectFileSearchIndexItemReadModel
                {
                    FileId = fileId,
                    ProjectId = Guid.NewGuid(),
                    ReferenceType = "PROJECT",
                    ReferenceId = Guid.NewGuid(),
                    OriginalFileName = "floor.pdf",
                    Status = FileStatus.ACTIVE
                },
                new ProjectFileSearchIndexItemReadModel
                {
                    FileId = blankFileId,
                    OriginalFileName = " "
                }
            ]),
            search,
            indexManager);

        await service.ReindexAccountsAsync();
        await service.ReindexProductsAsync();
        await service.ReindexProjectsAsync();
        await service.ReindexChatMessagesAsync();
        await service.ReindexProjectFilesAsync();

        Assert.Equal(["accounts", "products", "projects", "chat-messages", "project-files"], indexManager.EnsuredIndices);
        Assert.Contains(search.BulkCalls, call => call.IndexName == "accounts" && call.Count == 100);
        Assert.Contains(search.BulkCalls, call => call.IndexName == "accounts" && call.Count == 1);
        Assert.Contains(search.BulkCalls, call => call.IndexName == "products" && call.Count == 1);
        Assert.Contains(search.BulkCalls, call => call.IndexName == "projects" && call.Count == 1);
        Assert.Contains(search.BulkCalls, call => call.IndexName == "chat-messages" && call.Count == 1);
        Assert.Contains(search.BulkCalls, call => call.IndexName == "project-files" && call.Count == 1);
        Assert.Contains(search.Deleted, item => item == ("products", hiddenProductId.ToString()));
        Assert.Contains(search.Deleted, item => item == ("chat-messages", blankMessageId.ToString()));
        Assert.Contains(search.Deleted, item => item == ("project-files", blankFileId.ToString()));
    }

    [Fact]
    public async Task ReindexAllAsync_WithEmptyPages_OnlyEnsuresIndices()
    {
        var search = new CapturingSearchIndexService();
        var indexManager = new CapturingIndexManager();
        var service = new SearchReindexService(
            new FakeAccountRepository([]),
            new FakeProductRepository([]),
            new FakeProjectRepository([]),
            new FakeChatMessageRepository([]),
            new FakeProjectFileRepository([]),
            search,
            indexManager);

        await service.ReindexAccountsAsync();
        await service.ReindexProductsAsync();
        await service.ReindexProjectsAsync();
        await service.ReindexChatMessagesAsync();
        await service.ReindexProjectFilesAsync();

        Assert.Equal(5, indexManager.EnsuredIndices.Count);
        Assert.Empty(search.BulkCalls);
        Assert.Empty(search.Deleted);
    }

    private static Account[] CreateAccounts(int count)
        => Enumerable.Range(1, count)
            .Select(index => new Account
            {
                AccountId = Guid.NewGuid(),
                RoleId = Guid.NewGuid(),
                Email = $"user{index}@example.com",
                FullName = $"User {index}",
                PasswordHash = "hash",
                Status = AccountStatus.ACTIVE
            })
            .ToArray();

    private sealed class CapturingSearchIndexService : ISearchIndexService
    {
        public List<(string IndexName, int Count)> BulkCalls { get; } = [];
        public List<(string IndexName, string Id)> Deleted { get; } = [];

        public Task BulkIndexAsync<TDocument>(string indexName, IReadOnlyList<BulkIndexItem<TDocument>> items, CancellationToken cancellationToken = default)
        {
            BulkCalls.Add((indexName, items.Count));
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string indexName, string id, CancellationToken cancellationToken = default)
        {
            Deleted.Add((indexName, id));
            return Task.CompletedTask;
        }

        public Task IndexAsync<TDocument>(string indexName, string id, TDocument document, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<SearchResult<TDocument>> SearchAsync<TDocument>(string indexName, SearchRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new SearchResult<TDocument>());
        public Task<IReadOnlyList<TDocument>> SearchAsync<TDocument>(string indexName, string query, int size = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TDocument>>([]);
        public Task<SuggestResult> SuggestAsync(string indexName, SuggestRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new SuggestResult());
        public Task<SearchResult<TDocument>> MoreLikeThisAsync<TDocument>(string indexName, string documentId, MoreLikeThisRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new SearchResult<TDocument>());
        public Task<SearchAggregationResult> AggregateAsync(string indexName, SearchAggregationRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new SearchAggregationResult());
    }

    private sealed class CapturingIndexManager : IIndexManager
    {
        public List<string> EnsuredIndices { get; } = [];
        public Task EnsureIndexAsync(string indexName, CancellationToken cancellationToken = default)
        {
            EnsuredIndices.Add(indexName);
            return Task.CompletedTask;
        }
        public Task<bool> IndexExistsAsync(string indexName, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task DeleteIndexAsync(string indexName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeAccountRepository : IAccountRepository
    {
        private readonly IReadOnlyList<Account> _accounts;
        public FakeAccountRepository(IReadOnlyList<Account> accounts) => _accounts = accounts;
        public Task<IReadOnlyList<Account>> GetPagedAsync(int page, int pageSize, string? search, string? status, bool includeDeleted, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Account>>(_accounts.Skip((page - 1) * pageSize).Take(pageSize).ToArray());
        public Task<Account?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult<Account?>(null);
        public Task<AccountDetailReadModel?> GetDetailAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult<AccountDetailReadModel?>(null);
        public Task<string?> GetRoleNameAsync(Guid roleId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<Guid?> GetRoleIdByNameAsync(string roleName, CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(null);
        public Task<bool> RoleExistsAsync(Guid roleId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> EmailExistsAsync(string email, Guid? excludedAccountId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<AvailableDesignerReadModel>> GetAvailableDesignersAsync(int page, int pageSize, int maxActiveProjects, string? search, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AvailableDesignerReadModel>>([]);
        public Task<int> CountAvailableDesignersAsync(int maxActiveProjects, string? search, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<AvailableDesignerReadModel>> GetDesignerWorkloadAsync(int page, int pageSize, int maxActiveProjects, string? search, string? capacityState, string sortBy, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AvailableDesignerReadModel>>([]);
        public Task<int> CountDesignerWorkloadAsync(int maxActiveProjects, string? search, string? capacityState, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<DesignerWorkloadSummaryReadModel> GetDesignerWorkloadSummaryAsync(int maxActiveProjects, CancellationToken cancellationToken = default) => Task.FromResult(new DesignerWorkloadSummaryReadModel());
        public Task<IReadOnlyList<DesignerAssignedProjectReadModel>> GetDesignerAssignedProjectsAsync(Guid designerId, int page, int pageSize, string? bucket, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DesignerAssignedProjectReadModel>>([]);
        public Task<int> CountDesignerAssignedProjectsAsync(Guid designerId, string? bucket, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<bool> IsActiveDesignerAsync(Guid designerId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<SalesWorkloadItemReadModel>> GetSalesWorkloadAsync(SalesWorkloadListQuery query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SalesWorkloadItemReadModel>>([]);
        public Task<int> CountSalesWorkloadAsync(int maxActiveProjects, string? search, string? capacityState, string? futurePressureState, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<SalesWorkloadSummaryReadModel> GetSalesWorkloadSummaryAsync(int maxActiveProjects, CancellationToken cancellationToken = default) => Task.FromResult(new SalesWorkloadSummaryReadModel());
        public Task<IReadOnlyList<SalesAssignedProjectReadModel>> GetSalesAssignedProjectsAsync(Guid salesId, int page, int pageSize, string? bucket, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SalesAssignedProjectReadModel>>([]);
        public Task<int> CountSalesAssignedProjectsAsync(Guid salesId, string? bucket, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<UnassignedIntakeProjectReadModel>> GetUnassignedIntakeProjectsAsync(int page, int pageSize, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<UnassignedIntakeProjectReadModel>>([]);
        public Task<int> CountUnassignedIntakeProjectsAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<bool> IsActiveSalesAsync(Guid salesId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<int> CountAsync(string? search, string? status, bool includeDeleted, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<AccountFacetCountReadModel>> CountGroupedByStatusAsync(bool includeDeleted, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AccountFacetCountReadModel>>([]);
        public Task<IReadOnlyList<AccountFacetCountReadModel>> CountGroupedByRoleIdAsync(bool includeDeleted, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AccountFacetCountReadModel>>([]);
        public IQueryable<Account> Query() => _accounts.AsQueryable();
        public Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Account?>(null);
        public Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult(_accounts);
        public Task AddAsync(Account entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<Account> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(Account entity) { }
        public void Remove(Account entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        private readonly IReadOnlyList<ProductListItemReadModel> _items;
        public FakeProductRepository(IReadOnlyList<ProductListItemReadModel> items) => _items = items;
        public Task<IReadOnlyList<ProductListItemReadModel>> GetSearchIndexPageAsync(int page, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductListItemReadModel>>(_items.Skip((page - 1) * limit).Take(limit).ToArray());
        public Task<ProductListItemReadModel?> GetSearchIndexItemAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<ProductListItemReadModel?>(null);
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

    private sealed class FakeProjectRepository : IProjectRepository
    {
        private readonly IReadOnlyList<ProjectSearchIndexItemReadModel> _items;
        public FakeProjectRepository(IReadOnlyList<ProjectSearchIndexItemReadModel> items) => _items = items;
        public Task<IReadOnlyList<ProjectSearchIndexItemReadModel>> GetSearchIndexPageAsync(int page, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectSearchIndexItemReadModel>>(_items.Skip((page - 1) * limit).Take(limit).ToArray());
        public Task<ProjectSearchIndexItemReadModel?> GetSearchIndexItemAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectSearchIndexItemReadModel?>(null);
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

    private sealed class FakeChatMessageRepository : IProjectChatMessageRepository
    {
        private readonly IReadOnlyList<ChatMessageSearchIndexItemReadModel> _items;
        public FakeChatMessageRepository(IReadOnlyList<ChatMessageSearchIndexItemReadModel> items) => _items = items;
        public Task<IReadOnlyList<ChatMessageSearchIndexItemReadModel>> GetSearchIndexPageAsync(int page, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ChatMessageSearchIndexItemReadModel>>(_items.Skip((page - 1) * limit).Take(limit).ToArray());
        public Task<ChatMessageSearchIndexItemReadModel?> GetSearchIndexItemAsync(Guid messageId, CancellationToken cancellationToken = default) => Task.FromResult<ChatMessageSearchIndexItemReadModel?>(null);
        public Task<ProjectChatMessageAccessReadModel?> GetAccessAsync(Guid chatId, Guid currentUserId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectChatMessageAccessReadModel?>(null);
        public Task<(IReadOnlyList<ProjectChatMessageReadModel> Items, int Total)> GetMessagesAsync(Guid chatId, ProjectChatMessageQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult<(IReadOnlyList<ProjectChatMessageReadModel>, int)>(([], 0));
        public Task<IReadOnlyList<ChatMessageSearchIndexItemReadModel>> SearchByProjectAsync(Guid projectId, string query, int page, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ChatMessageSearchIndexItemReadModel>>([]);
        public Task<int> CountSearchByProjectAsync(Guid projectId, string query, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public IQueryable<ProjectChatMessage> Query() => Enumerable.Empty<ProjectChatMessage>().AsQueryable();
        public Task<ProjectChatMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<ProjectChatMessage?>(null);
        public Task<IReadOnlyList<ProjectChatMessage>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectChatMessage>>([]);
        public Task AddAsync(ProjectChatMessage entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<ProjectChatMessage> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(ProjectChatMessage entity) { }
        public void Remove(ProjectChatMessage entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class FakeProjectFileRepository : IProjectFileRepository
    {
        private readonly IReadOnlyList<ProjectFileSearchIndexItemReadModel> _items;
        public FakeProjectFileRepository(IReadOnlyList<ProjectFileSearchIndexItemReadModel> items) => _items = items;
        public Task<IReadOnlyList<ProjectFileSearchIndexItemReadModel>> GetSearchIndexPageAsync(int page, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectFileSearchIndexItemReadModel>>(_items.Skip((page - 1) * limit).Take(limit).ToArray());
        public Task<ProjectFileSearchIndexItemReadModel?> GetSearchIndexItemAsync(Guid fileId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectFileSearchIndexItemReadModel?>(null);
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
        public Task<ProjectLinkedFileReadModel?> GetProjectLinkedActiveFileAsync(Guid projectId, Guid fileId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectLinkedFileReadModel?>(null);
        public IQueryable<StoredFile> Query() => Enumerable.Empty<StoredFile>().AsQueryable();
        public Task<StoredFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<StoredFile?>(null);
        public Task<IReadOnlyList<StoredFile>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StoredFile>>([]);
        public Task AddAsync(StoredFile entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<StoredFile> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(StoredFile entity) { }
        public void Remove(StoredFile entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }
}
