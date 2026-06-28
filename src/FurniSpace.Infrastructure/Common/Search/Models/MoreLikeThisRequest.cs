namespace FurniSpace.Infrastructure.Common.Search;

public sealed class MoreLikeThisRequest
{
    public IReadOnlyList<string> Fields { get; init; } = [];

    public IReadOnlyList<SearchFilter> Filters { get; init; } = [];

    public int Size { get; init; } = 10;
}
