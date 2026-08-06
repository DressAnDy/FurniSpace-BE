using FurniSpace.Infrastructure.Common.Search;
using FurniSpace.Infrastructure.Interfaces;

namespace FurniSpace.Testing.Fakes;

public sealed class CoreSearchIndexService : ISearchIndexService
{
    public Task IndexAsync<TDocument>(
        string indexName,
        string id,
        TDocument document,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task BulkIndexAsync<TDocument>(
        string indexName,
        IReadOnlyList<BulkIndexItem<TDocument>> items,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task DeleteAsync(
        string indexName,
        string id,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<SearchResult<TDocument>> SearchAsync<TDocument>(
        string indexName,
        SearchRequest request,
        CancellationToken cancellationToken = default) =>
        throw SearchUnavailable();

    public Task<IReadOnlyList<TDocument>> SearchAsync<TDocument>(
        string indexName,
        string query,
        int size = 100,
        CancellationToken cancellationToken = default) =>
        throw SearchUnavailable();

    public Task<SuggestResult> SuggestAsync(
        string indexName,
        SuggestRequest request,
        CancellationToken cancellationToken = default) =>
        throw SearchUnavailable();

    public Task<SearchResult<TDocument>> MoreLikeThisAsync<TDocument>(
        string indexName,
        string documentId,
        MoreLikeThisRequest request,
        CancellationToken cancellationToken = default) =>
        throw SearchUnavailable();

    public Task<SearchAggregationResult> AggregateAsync(
        string indexName,
        SearchAggregationRequest request,
        CancellationToken cancellationToken = default) =>
        throw SearchUnavailable();

    private static NotSupportedException SearchUnavailable() =>
        new("Elasticsearch is disabled in the core integration suite.");
}

public sealed class NoOpIndexManager : IIndexManager
{
    public Task EnsureIndexAsync(string indexName, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<bool> IndexExistsAsync(string indexName, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task DeleteIndexAsync(string indexName, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
