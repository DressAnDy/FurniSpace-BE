using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using FurniSpace.Infrastructure.Common.Search;
using FurniSpace.Infrastructure.Interfaces;
using IndexSearchRequest = FurniSpace.Infrastructure.Common.Search.SearchRequest;
using Microsoft.Extensions.Options;

namespace FurniSpace.Infrastructure.Common.Search;

public sealed class ElasticsearchIndexService : ISearchIndexService
{
    private readonly ElasticsearchClient _client;
    private readonly ElasticsearchIndexNameBuilder _indexNames;

    public ElasticsearchIndexService(
        ElasticsearchClient client,
        IOptions<ElasticsearchSettings> settings)
    {
        _client = client;
        _indexNames = new ElasticsearchIndexNameBuilder(settings.Value);
    }

    public async Task IndexAsync<TDocument>(
        string indexName,
        string id,
        TDocument document,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.IndexAsync(document, _indexNames.Build(indexName), id, cancellationToken);

        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException(response.DebugInformation);
        }
    }

    public async Task BulkIndexAsync<TDocument>(
        string indexName,
        IReadOnlyList<BulkIndexItem<TDocument>> items,
        CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            await IndexAsync(indexName, item.Id, item.Document, cancellationToken);
        }
    }

    public async Task DeleteAsync(
        string indexName,
        string id,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.DeleteAsync<object>(_indexNames.Build(indexName), id, cancellationToken);

        if (!response.IsValidResponse && response.ApiCallDetails.HttpStatusCode is not 404)
        {
            throw new InvalidOperationException(response.DebugInformation);
        }
    }

    public async Task<SearchResult<TDocument>> SearchAsync<TDocument>(
        string indexName,
        IndexSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.SearchAsync<TDocument>(
            s => ElasticsearchQueryBuilder.ApplySearchRequest(s.Indices(_indexNames.Build(indexName)), request),
            cancellationToken);

        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException(response.DebugInformation);
        }

        return new SearchResult<TDocument>
        {
            Documents = response.Documents.ToArray(),
            Total = response.Total,
            Page = request.Page,
            PageSize = request.PageSize,
            Facets = ElasticsearchAggregationHelper.ParseTermsAggregations(
                response.Aggregations,
                request.FacetFields)
        };
    }

    public async Task<IReadOnlyList<TDocument>> SearchAsync<TDocument>(
        string indexName,
        string query,
        int size = 100,
        CancellationToken cancellationToken = default)
    {
        var result = await SearchAsync<TDocument>(
            indexName,
            new IndexSearchRequest
            {
                Query = query,
                Page = 1,
                PageSize = size,
                TrackTotalHits = false
            },
            cancellationToken);

        return result.Documents;
    }

    public Task<SuggestResult> SuggestAsync(
        string indexName,
        SuggestRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = indexName;
        _ = request;
        _ = cancellationToken;

        return Task.FromResult(new SuggestResult());
    }

    public async Task<SearchResult<TDocument>> MoreLikeThisAsync<TDocument>(
        string indexName,
        string documentId,
        MoreLikeThisRequest request,
        CancellationToken cancellationToken = default)
    {
        var resolvedIndexName = _indexNames.Build(indexName);
        var fields = request.Fields.Count > 0
            ? request.Fields.ToArray()
            : new[] { "description", "material", "categoryName" };

        var response = await _client.SearchAsync<TDocument>(s => s
                .Indices(resolvedIndexName)
                .Size(request.Size)
                .Query(q => q.Bool(b => b
                    .Must(m => m.MoreLikeThis(mlt => mlt
                        .Fields(Fields.FromStrings(fields))
                        .Like(l => l.Document(doc => doc
                            .Index(resolvedIndexName)
                            .Id(documentId)))
                        .MinTermFreq(1)
                        .MaxQueryTerms(12)))
                    .MustNot(mn => mn.Ids(ids => ids.Values(documentId)))
                    .Filter(ElasticsearchQueryBuilder.CreateFilterQueries(request.Filters)))),
            cancellationToken);

        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException(response.DebugInformation);
        }

        return new SearchResult<TDocument>
        {
            Documents = response.Documents.ToArray(),
            Total = response.Total,
            Page = 1,
            PageSize = request.Size
        };
    }

    public async Task<SearchAggregationResult> AggregateAsync(
        string indexName,
        SearchAggregationRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.SearchAsync<object>(s =>
        {
            var descriptor = s.Indices(_indexNames.Build(indexName));
            ElasticsearchQueryBuilder.ApplyAggregationRequest(descriptor, request);
            ElasticsearchAggregationHelper.ApplyTermsAggregations(
                descriptor,
                request.TermsFields,
                request.TermsSize);
        }, cancellationToken);

        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException(response.DebugInformation);
        }

        return new SearchAggregationResult
        {
            Facets = ElasticsearchAggregationHelper.ParseTermsAggregations(
                response.Aggregations,
                request.TermsFields)
        };
    }
}
