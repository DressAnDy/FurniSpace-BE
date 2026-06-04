namespace FurniSpace.Infrastructure.Common.Caching;

public sealed class RedisSettings
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = string.Empty;
}
