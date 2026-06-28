using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Search;
using FurniSpace.Infrastructure.ReadModels.Projects;

namespace FurniSpace.Application.Services.Search;

public static class ProjectElasticsearchQueryFactory
{
    public static SearchRequest Build(ProjectListQueryReadModel query)
    {
        var filters = new List<SearchFilter>();

        if (query.Status.HasValue)
        {
            filters.Add(new SearchFilter("status", SearchFilterOperator.Term, query.Status.Value.ToString()));
        }

        if (query.AssignedSalesId.HasValue)
        {
            filters.Add(new SearchFilter(
                "assignedSalesId",
                SearchFilterOperator.Term,
                query.AssignedSalesId.Value.ToString()));
        }

        if (query.AssignedDesignerId.HasValue)
        {
            filters.Add(new SearchFilter(
                "assignedDesignerId",
                SearchFilterOperator.Term,
                query.AssignedDesignerId.Value.ToString()));
        }

        if (query.CustomerId.HasValue)
        {
            filters.Add(new SearchFilter(
                "customerId",
                SearchFilterOperator.Term,
                query.CustomerId.Value.ToString()));
        }

        return new SearchRequest
        {
            Query = BuildQueryString(query.Search),
            Page = query.Page,
            PageSize = query.Limit,
            Filters = filters,
            Sort =
            [
                new SearchSortField("submittedAt", SortDirection.Desc),
                new SearchSortField("projectId", SortDirection.Desc)
            ],
            TrackTotalHits = true
        };
    }

    private static string? BuildQueryString(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var value = EscapeQueryString(search.Trim());
        return $"projectCode:*{value}* OR projectName:*{value}* OR customerName:*{value}* OR customerEmail:*{value}* OR customerPhone:*{value}*";
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
