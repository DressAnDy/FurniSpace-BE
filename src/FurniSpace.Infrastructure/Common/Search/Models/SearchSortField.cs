namespace FurniSpace.Infrastructure.Common.Search;

public sealed record SearchSortField(string Field, SortDirection Direction = SortDirection.Asc);
