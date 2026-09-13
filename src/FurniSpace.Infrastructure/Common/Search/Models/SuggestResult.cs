namespace FurniSpace.Infrastructure.Common.Search;

public sealed class SuggestResult
{
    public IReadOnlyList<string> Suggestions { get; init; } = [];
}
