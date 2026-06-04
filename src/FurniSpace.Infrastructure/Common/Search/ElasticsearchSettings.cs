namespace FurniSpace.Infrastructure.Common.Search;

public sealed class ElasticsearchSettings
{
    public const string SectionName = "Elasticsearch";

    public string Url { get; set; } = string.Empty;
    public string IndexPrefix { get; set; } = "furnispace";
}
