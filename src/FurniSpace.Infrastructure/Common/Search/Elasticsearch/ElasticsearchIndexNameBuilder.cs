using FurniSpace.Infrastructure.Common.Search;

namespace FurniSpace.Infrastructure.Common.Search;

public sealed class ElasticsearchIndexNameBuilder
{
    private readonly ElasticsearchSettings _settings;

    public ElasticsearchIndexNameBuilder(ElasticsearchSettings settings)
    {
        _settings = settings;
    }

    public string Build(string indexName)
    {
        if (string.IsNullOrWhiteSpace(indexName))
        {
            throw new ArgumentException("Index name is required.", nameof(indexName));
        }

        if (indexName.StartsWith($"{_settings.IndexPrefix}-", StringComparison.OrdinalIgnoreCase))
        {
            return indexName;
        }

        return $"{_settings.IndexPrefix}-{indexName}";
    }
}
