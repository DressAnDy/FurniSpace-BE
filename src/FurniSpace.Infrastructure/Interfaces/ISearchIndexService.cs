using FurniSpace.Infrastructure.Common.Search;

namespace FurniSpace.Infrastructure.Interfaces;

public interface ISearchIndexService
{
    Task IndexAsync<TDocument>(
        string indexName,
        string id,
        TDocument document,
        CancellationToken cancellationToken = default);

    Task BulkIndexAsync<TDocument>(
        string indexName,
        IReadOnlyList<BulkIndexItem<TDocument>> items,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string indexName,
        string id,
        CancellationToken cancellationToken = default);

    Task<SearchResult<TDocument>> SearchAsync<TDocument>(
        string indexName,
        SearchRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TDocument>> SearchAsync<TDocument>(
        string indexName,
        string query,
        int size = 100,
        CancellationToken cancellationToken = default);

    Task<SuggestResult> SuggestAsync(
        string indexName,
        SuggestRequest request,
        CancellationToken cancellationToken = default);

    Task<SearchResult<TDocument>> MoreLikeThisAsync<TDocument>(
        string indexName,
        string documentId,
        MoreLikeThisRequest request,
        CancellationToken cancellationToken = default);

    Task<SearchAggregationResult> AggregateAsync(
        string indexName,
        SearchAggregationRequest request,
        CancellationToken cancellationToken = default);
}
