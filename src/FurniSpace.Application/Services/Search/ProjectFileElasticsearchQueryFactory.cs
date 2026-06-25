using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Search;

namespace FurniSpace.Application.Services.Search;

public static class ProjectFileElasticsearchQueryFactory
{
    public static SearchRequest BuildProjectSearch(
        Guid projectId,
        string query,
        int page,
        int limit,
        bool customerVisibleOnly,
        Guid? customerAccountId = null)
    {
        var filters = new List<SearchFilter>
        {
            new("projectId", SearchFilterOperator.Term, projectId.ToString())
        };

        IReadOnlyList<SearchFilterGroup> shouldGroups = [];
        if (customerVisibleOnly)
        {
            if (customerAccountId.HasValue)
            {
                shouldGroups =
                [
                    new SearchFilterGroup(
                    [
                        new SearchFilter(
                            "visibility",
                            SearchFilterOperator.Term,
                            nameof(FileVisibility.CUSTOMER_VISIBLE)),
                        new SearchFilter(
                            "uploadedBy",
                            SearchFilterOperator.Term,
                            customerAccountId.Value.ToString())
                    ])
                ];
            }
            else
            {
                filters.Add(new SearchFilter(
                    "visibility",
                    SearchFilterOperator.Term,
                    nameof(FileVisibility.CUSTOMER_VISIBLE)));
            }
        }

        return new SearchRequest
        {
            Query = BuildQueryString(query),
            Page = page,
            PageSize = limit,
            Filters = filters,
            FilterShouldMatchOne = shouldGroups,
            Sort =
            [
                new SearchSortField("uploadedAt", SortDirection.Desc),
                new SearchSortField("fileId", SortDirection.Desc)
            ],
            TrackTotalHits = true
        };
    }

    private static string? BuildQueryString(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var value = EscapeQueryString(query.Trim());
        return $"originalFileName:*{value}*";
    }

    private static string EscapeQueryString(string value)
    {
        var reservedChars = new[] { "\\", "+", "-", "=", "&&", "||", ">", "<", "!", "(", ")", "{", "}", "[", "]", "^", "\"", "~", "*", "?", ":", "/", " " };
        var escaped = value;
        foreach (var reservedChar in reservedChars)
        {
            escaped = escaped.Replace(reservedChar, $"\\{reservedChar}", StringComparison.Ordinal);
        }

        return escaped;
    }
}
