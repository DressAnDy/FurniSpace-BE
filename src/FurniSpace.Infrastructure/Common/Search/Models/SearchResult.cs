namespace FurniSpace.Infrastructure.Common.Search;

public sealed class SearchResult<TDocument>
{
    public IReadOnlyList<TDocument> Documents { get; init; } = [];

    public long Total { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }

    public IReadOnlyDictionary<string, IReadOnlyList<SearchFacetBucket>> Facets { get; init; }
        = new Dictionary<string, IReadOnlyList<SearchFacetBucket>>();
}
