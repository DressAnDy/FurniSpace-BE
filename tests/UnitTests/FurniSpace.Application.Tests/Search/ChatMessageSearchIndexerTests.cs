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
using FurniSpace.Infrastructure.ReadModels.ProjectChatMessages;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Search;

public sealed class ChatMessageSearchIndexerTests
{
    [Fact]
    public async Task SyncMessageAsync_WithIndexableMessage_IndexesDocument()
    {
        var messageId = Guid.NewGuid();
        var repository = new FakeChatMessageRepository(new ChatMessageSearchIndexItemReadModel
        {
            MessageId = messageId,
            ChatId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            SenderId = Guid.NewGuid(),
            SenderName = "Designer",
            MessageType = ProjectChatMessageType.TEXT,
            Content = "  Please review  ",
            CreatedAt = DateTime.UtcNow
        });
        var search = new CapturingSearchIndexService();
        var indexer = new ChatMessageSearchIndexer(repository, search);

        await indexer.SyncMessageAsync(messageId);

        Assert.Equal(1, repository.GetSearchIndexItemCallCount);
        Assert.Equal("chat-messages", search.IndexName);
        Assert.Equal(messageId.ToString(), search.IndexId);
        var document = Assert.IsType<ChatMessageSearchDocument>(search.IndexedDocument);
        Assert.Equal("Please review", document.Content);
        Assert.Equal(0, search.DeleteCallCount);
    }

    [Fact]
    public async Task SyncMessageAsync_WithMissingOrNonIndexableMessage_DeletesDocument()
    {
        var messageId = Guid.NewGuid();
        var search = new CapturingSearchIndexService();
        var missingIndexer = new ChatMessageSearchIndexer(new FakeChatMessageRepository(null), search);

        await missingIndexer.SyncMessageAsync(messageId);

        Assert.Equal("chat-messages", search.DeleteIndexName);
        Assert.Equal(messageId.ToString(), search.DeleteId);

        var blankSearch = new CapturingSearchIndexService();
        var blankIndexer = new ChatMessageSearchIndexer(
            new FakeChatMessageRepository(new ChatMessageSearchIndexItemReadModel
            {
                MessageId = messageId,
                Content = " "
            }),
            blankSearch);

        await blankIndexer.SyncMessageAsync(messageId);

        Assert.Equal(1, blankSearch.DeleteCallCount);
        Assert.Equal(0, blankSearch.IndexCallCount);
    }

    [Fact]
    public async Task SyncMessageAsync_WhenRepositoryOrSearchThrows_DoesNotBubble()
    {
        var repository = new FakeChatMessageRepository(null, throwOnGet: true);
        var search = new CapturingSearchIndexService(throwOnIndex: true, throwOnDelete: true);
        var indexer = new ChatMessageSearchIndexer(repository, search);

        await indexer.SyncMessageAsync(Guid.NewGuid());

        Assert.Equal(1, repository.GetSearchIndexItemCallCount);
        Assert.Equal(0, search.IndexCallCount);
        Assert.Equal(0, search.DeleteCallCount);
    }

    private sealed class FakeChatMessageRepository : IProjectChatMessageRepository
    {
        private readonly ChatMessageSearchIndexItemReadModel? _item;
        private readonly bool _throwOnGet;
        private readonly List<ProjectChatMessage> _entities = [];

        public FakeChatMessageRepository(ChatMessageSearchIndexItemReadModel? item, bool throwOnGet = false)
        {
            _item = item;
            _throwOnGet = throwOnGet;
        }

        public int GetSearchIndexItemCallCount { get; private set; }

        public Task<ChatMessageSearchIndexItemReadModel?> GetSearchIndexItemAsync(
            Guid messageId,
            CancellationToken cancellationToken = default)
        {
            GetSearchIndexItemCallCount++;
            if (_throwOnGet)
            {
                throw new InvalidOperationException("Repository unavailable.");
            }

            return Task.FromResult(_item?.MessageId == messageId ? _item : null);
        }

        public Task<ProjectChatMessageAccessReadModel?> GetAccessAsync(
            Guid chatId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectChatMessageAccessReadModel?>(null);

        public Task<(IReadOnlyList<ProjectChatMessageReadModel> Items, int Total)> GetMessagesAsync(
            Guid chatId,
            ProjectChatMessageQueryReadModel query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<(IReadOnlyList<ProjectChatMessageReadModel>, int)>(([], 0));

        public Task<IReadOnlyList<ChatMessageSearchIndexItemReadModel>> GetSearchIndexPageAsync(
            int page,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ChatMessageSearchIndexItemReadModel>>([]);

        public Task<IReadOnlyList<ChatMessageSearchIndexItemReadModel>> SearchByProjectAsync(
            Guid projectId,
            string query,
            int page,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ChatMessageSearchIndexItemReadModel>>([]);

        public Task<int> CountSearchByProjectAsync(
            Guid projectId,
            string query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public IQueryable<ProjectChatMessage> Query() => _entities.AsQueryable();
        public Task<ProjectChatMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectChatMessage?>(null);
        public Task<IReadOnlyList<ProjectChatMessage>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectChatMessage>>(_entities);
        public Task AddAsync(ProjectChatMessage entity, CancellationToken cancellationToken = default)
        {
            _entities.Add(entity);
            return Task.CompletedTask;
        }
        public Task AddRangeAsync(IEnumerable<ProjectChatMessage> entities, CancellationToken cancellationToken = default)
        {
            _entities.AddRange(entities);
            return Task.CompletedTask;
        }
        public void Update(ProjectChatMessage entity) { }
        public void Remove(ProjectChatMessage entity) => _entities.Remove(entity);
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
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
}
