using System.Reflection;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using FurniSpace.Infrastructure.Common.Search;
using FurniSpace.Infrastructure.Interfaces;
using Microsoft.Extensions.Options;

namespace FurniSpace.Infrastructure.Common.Search;

public sealed class ElasticsearchIndexManager : IIndexManager
{
    private const string AccountsMappingResourceName =
        "FurniSpace.Infrastructure.Common.Search.Mappings.accounts-index.json";

    private const string ProductsMappingResourceName =
        "FurniSpace.Infrastructure.Common.Search.Mappings.products-index.json";

    private const string ProjectsMappingResourceName =
        "FurniSpace.Infrastructure.Common.Search.Mappings.projects-index.json";

    private const string ChatMessagesMappingResourceName =
        "FurniSpace.Infrastructure.Common.Search.Mappings.chat-messages-index.json";

    private const string ProjectFilesMappingResourceName =
        "FurniSpace.Infrastructure.Common.Search.Mappings.project-files-index.json";

    private static readonly Dictionary<string, string> IndexMappingResources =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["accounts"] = AccountsMappingResourceName,
            ["products"] = ProductsMappingResourceName,
            ["projects"] = ProjectsMappingResourceName,
            ["chat-messages"] = ChatMessagesMappingResourceName,
            ["project-files"] = ProjectFilesMappingResourceName
        };

    private readonly ElasticsearchClient _client;
    private readonly ElasticsearchIndexNameBuilder _indexNames;

    public ElasticsearchIndexManager(
        ElasticsearchClient client,
        IOptions<ElasticsearchSettings> settings)
    {
        _client = client;
        _indexNames = new ElasticsearchIndexNameBuilder(settings.Value);
    }

    public async Task EnsureIndexAsync(string indexName, CancellationToken cancellationToken = default)
    {
        if (await IndexExistsAsync(indexName, cancellationToken))
        {
            return;
        }

        if (!IndexMappingResources.TryGetValue(indexName, out var resourceName))
        {
            throw new InvalidOperationException($"No Elasticsearch mapping is registered for index '{indexName}'.");
        }

        var mappingJson = ReadEmbeddedMapping(resourceName);
        var resolvedIndexName = _indexNames.Build(indexName);

        var response = await _client.Transport.RequestAsync<StringResponse>(
            Elastic.Transport.HttpMethod.PUT,
            resolvedIndexName,
            PostData.String(mappingJson),
            cancellationToken);

        if (!response.ApiCallDetails.HasSuccessfulStatusCode)
        {
            throw new InvalidOperationException(response.Body ?? "Failed to create Elasticsearch index.");
        }
    }

    public async Task<bool> IndexExistsAsync(string indexName, CancellationToken cancellationToken = default)
    {
        var resolvedIndexName = _indexNames.Build(indexName);
        var response = await _client.Indices.ExistsAsync(resolvedIndexName, cancellationToken);
        return response.Exists;
    }

    public async Task DeleteIndexAsync(string indexName, CancellationToken cancellationToken = default)
    {
        var resolvedIndexName = _indexNames.Build(indexName);
        var response = await _client.Indices.DeleteAsync(resolvedIndexName, cancellationToken);

        if (!response.IsValidResponse && response.ApiCallDetails.HttpStatusCode is not 404)
        {
            throw new InvalidOperationException(response.DebugInformation);
        }
    }

    private static string ReadEmbeddedMapping(string resourceName)
    {
        var assembly = typeof(ElasticsearchIndexManager).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded mapping resource '{resourceName}' was not found.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
