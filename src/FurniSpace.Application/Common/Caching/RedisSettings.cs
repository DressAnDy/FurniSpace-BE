namespace FurniSpace.Application.Common.Caching;

public sealed class RedisSettings
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = string.Empty;
    public string InstanceName { get; set; } = "FurniSpace";
    public bool IsEnabled { get; set; } = true;
}
