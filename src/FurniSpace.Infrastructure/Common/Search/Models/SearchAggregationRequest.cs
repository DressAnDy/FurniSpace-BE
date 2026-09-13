namespace FurniSpace.Infrastructure.Common.Search;

public sealed class SearchAggregationRequest
{
    public string? Query { get; init; }

    public IReadOnlyList<SearchFilter> Filters { get; init; } = [];

    public IReadOnlyList<string> TermsFields { get; init; } = [];

    public int TermsSize { get; init; } = 50;
}
