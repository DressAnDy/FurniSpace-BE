namespace FurniSpace.Infrastructure.Common.Search;

public sealed class SearchAggregationResult
{
    public IReadOnlyDictionary<string, IReadOnlyList<SearchFacetBucket>> Facets { get; init; }
        = new Dictionary<string, IReadOnlyList<SearchFacetBucket>>();
}
