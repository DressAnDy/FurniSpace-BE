namespace FurniSpace.Infrastructure.Common.Logging;

public sealed class ElasticsearchLogSettings
{
    public const string SectionName = "ElasticsearchLogging";

    public bool Enabled { get; set; }

    public string IndexFormat { get; set; } = "furnispace-logs-{0:yyyy.MM}";
}
