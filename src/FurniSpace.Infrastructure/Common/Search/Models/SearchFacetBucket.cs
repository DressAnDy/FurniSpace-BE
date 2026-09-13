namespace FurniSpace.Infrastructure.Common.Search;

public sealed class SearchFacetBucket
{
    public string Key { get; set; } = string.Empty;

    public long Count { get; set; }
}
