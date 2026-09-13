namespace FurniSpace.Application.DTOs.Search;

public sealed class SearchFacetItemDto
{
    public string Key { get; set; } = string.Empty;

    public long Count { get; set; }

    public string? Label { get; set; }
}
