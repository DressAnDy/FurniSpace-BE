namespace FurniSpace.Infrastructure.Common.Search;

public sealed record SearchFilterGroup(IReadOnlyList<SearchFilter> AnyOf);
