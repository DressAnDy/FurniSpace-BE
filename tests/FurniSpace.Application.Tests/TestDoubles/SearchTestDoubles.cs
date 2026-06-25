#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Interfaces.Search;
using FurniSpace.Infrastructure.Common.Search;
using FurniSpace.Infrastructure.Interfaces;

namespace FurniSpace.Application.Tests.TestDoubles;

public sealed class NoOpProductSearchIndexer : IProductSearchIndexer
{
    public Task SyncProductAsync(Guid productId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public sealed class NoOpProjectSearchIndexer : IProjectSearchIndexer
{
    public Task SyncProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public sealed class NoOpChatMessageSearchIndexer : IChatMessageSearchIndexer
{
    public Task SyncMessageAsync(Guid messageId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public sealed class NoOpProjectFileSearchIndexer : IProjectFileSearchIndexer
{
    public Task SyncFileAsync(Guid fileId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public sealed class NoOpSearchIndexService : ISearchIndexService
{
    public Task IndexAsync<TDocument>(string indexName, string id, TDocument document, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task BulkIndexAsync<TDocument>(string indexName, IReadOnlyList<BulkIndexItem<TDocument>> items, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeleteAsync(string indexName, string id, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<SearchResult<TDocument>> SearchAsync<TDocument>(string indexName, SearchRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SearchResult<TDocument>
        {
            Documents = [],
            Total = 0,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }

    public Task<IReadOnlyList<TDocument>> SearchAsync<TDocument>(string indexName, string query, int size = 100, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TDocument>>([]);

    public Task<SuggestResult> SuggestAsync(string indexName, SuggestRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new SuggestResult());

    public Task<SearchResult<TDocument>> MoreLikeThisAsync<TDocument>(
        string indexName,
        string documentId,
        MoreLikeThisRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = indexName;
        _ = documentId;
        _ = request;
        _ = cancellationToken;

        return Task.FromResult(new SearchResult<TDocument>
        {
            Documents = [],
            Total = 0,
            Page = 1,
            PageSize = request.Size
        });
    }

    public Task<SearchAggregationResult> AggregateAsync(
        string indexName,
        SearchAggregationRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = indexName;
        _ = request;
        _ = cancellationToken;
        return Task.FromResult(new SearchAggregationResult());
    }
}
