using Elastic.Clients.Elasticsearch;
using FurniSpace.Infrastructure.Common.Search;
using FurniSpace.Infrastructure.Interfaces;
using Microsoft.Extensions.Options;

namespace FurniSpace.Infrastructure.Search;

public sealed class ElasticsearchIndexService : ISearchIndexService
{
    private readonly ElasticsearchClient _client;
    private readonly ElasticsearchSettings _settings;

    public ElasticsearchIndexService(
        ElasticsearchClient client,
        IOptions<ElasticsearchSettings> settings)
    {
        _client = client;
        _settings = settings.Value;
    }

    public async Task IndexAsync<TDocument>(
        string indexName,
        string id,
        TDocument document,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.IndexAsync(document, BuildIndexName(indexName), id, cancellationToken);

        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException(response.DebugInformation);
        }
    }

    public async Task DeleteAsync(
        string indexName,
        string id,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.DeleteAsync<object>(BuildIndexName(indexName), id, cancellationToken);

        if (!response.IsValidResponse && response.ApiCallDetails.HttpStatusCode is not 404)
        {
            throw new InvalidOperationException(response.DebugInformation);
        }
    }

    public async Task<IReadOnlyList<TDocument>> SearchAsync<TDocument>(
        string indexName,
        string query,
        int size = 100,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.SearchAsync<TDocument>(s => s
                .Indices(BuildIndexName(indexName))
                .Size(size)
                .Query(q => q.QueryString(qs => qs.Query(query))),
            cancellationToken);

        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException(response.DebugInformation);
        }

        return response.Documents.ToArray();
    }

    private string BuildIndexName(string indexName)
    {
        if (indexName.StartsWith($"{_settings.IndexPrefix}-", StringComparison.OrdinalIgnoreCase))
        {
            return indexName;
        }

        return $"{_settings.IndexPrefix}-{indexName}";
    }
}
