namespace FurniSpace.Infrastructure.Common.Search;

public sealed class SuggestRequest
{
    public required string Text { get; init; }

    public required string Field { get; init; }

    public int Size { get; init; } = 10;
}
