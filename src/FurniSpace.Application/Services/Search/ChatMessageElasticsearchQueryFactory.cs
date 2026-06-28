using FurniSpace.Infrastructure.Common.Search;

namespace FurniSpace.Application.Services.Search;

public static class ChatMessageElasticsearchQueryFactory
{
    public static SearchRequest BuildProjectSearch(Guid projectId, string query, int page, int limit)
    {
        return new SearchRequest
        {
            Query = BuildQueryString(query),
            Page = page,
            PageSize = limit,
            Filters =
            [
                new SearchFilter("projectId", SearchFilterOperator.Term, projectId.ToString())
            ],
            Sort =
            [
                new SearchSortField("createdAt", SortDirection.Desc),
                new SearchSortField("messageId", SortDirection.Desc)
            ],
            TrackTotalHits = true
        };
    }

    private static string BuildQueryString(string query)
    {
        var value = EscapeQueryString(query.Trim());
        return $"content:*{value}* OR senderName:*{value}*";
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
