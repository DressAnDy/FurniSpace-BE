namespace FurniSpace.Infrastructure.Common.Search;

public sealed class SearchRequest
{
    public string? Query { get; init; }

    public string? AutocompleteText { get; init; }

    public IReadOnlyList<string> AutocompleteFields { get; init; } = [];

    public IReadOnlyList<SearchFilter> Filters { get; init; } = [];

    public IReadOnlyList<SearchFilterGroup> FilterShouldMatchOne { get; init; } = [];

    public IReadOnlyList<SearchSortField> Sort { get; init; } = [];

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public bool TrackTotalHits { get; init; } = true;

    public IReadOnlyList<string> FacetFields { get; init; } = [];
}
