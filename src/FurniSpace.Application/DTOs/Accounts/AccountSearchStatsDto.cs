using FurniSpace.Application.DTOs.Search;

namespace FurniSpace.Application.DTOs.Accounts;

public sealed class AccountSearchStatsDto
{
    public IReadOnlyList<SearchFacetItemDto> StatusCounts { get; set; } = [];

    public IReadOnlyList<SearchFacetItemDto> RoleCounts { get; set; } = [];
}
