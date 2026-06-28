using FurniSpace.Infrastructure.Common.Search;

namespace FurniSpace.Application.Services.Search;

public static class AccountElasticsearchQueryFactory
{
    public const string StatusField = "status";
    public const string RoleIdField = "roleId";

    public static SearchRequest BuildSearch(
        int page,
        int pageSize,
        string search,
        string? status,
        bool includeDeleted)
    {
        return new SearchRequest
        {
            Query = BuildQueryString(search),
            Page = page,
            PageSize = pageSize,
            Filters = CreateAccountFilters(status, includeDeleted),
            Sort =
            [
                new SearchSortField("createdAt", SortDirection.Desc),
                new SearchSortField("email.keyword", SortDirection.Asc)
            ],
            TrackTotalHits = true
        };
    }

    public static SearchAggregationRequest BuildStatsAggregation(bool includeDeleted)
    {
        return new SearchAggregationRequest
        {
            Filters = CreateAccountFilters(status: null, includeDeleted),
            TermsFields = [StatusField, RoleIdField],
            TermsSize = 20
        };
    }

    public static SearchRequest BuildSuggest(string query, int limit, bool includeDeleted = false)
    {
        return new SearchRequest
        {
            Query = BuildQueryString(query),
            Page = 1,
            PageSize = limit,
            Filters = CreateAccountFilters(status: null, includeDeleted),
            Sort =
            [
                new SearchSortField("fullName.keyword", SortDirection.Asc),
                new SearchSortField("email.keyword", SortDirection.Asc)
            ],
            TrackTotalHits = false
        };
    }

    public static IReadOnlyList<SearchFilter> CreateAccountFilters(string? status, bool includeDeleted)
    {
        var filters = new List<SearchFilter>();

        if (!includeDeleted)
        {
            filters.Add(new SearchFilter("deletedAt", SearchFilterOperator.NotExists));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filters.Add(new SearchFilter(StatusField, SearchFilterOperator.Term, status));
        }

        return filters;
    }

    private static string BuildQueryString(string search)
    {
        var value = EscapeQueryString(search);
        return $"email:*{value}* OR fullName:*{value}* OR phone:*{value}*";
    }

    private static string EscapeQueryString(string value)
    {
        var reservedChars = new[] { "\\", "+", "-", "=", "&&", "||", ">", "<", "!", "(", ")", "{", "}", "[", "]", "^", "\"", "~", "*", "?", ":", "/", " " };
        var escaped = value.Trim();
        foreach (var reservedChar in reservedChars)
        {
            escaped = escaped.Replace(reservedChar, $"\\{reservedChar}", StringComparison.Ordinal);
        }

        return escaped;
    }
}
