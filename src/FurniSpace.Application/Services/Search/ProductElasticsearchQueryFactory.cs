using FurniSpace.Application.DTOs.Products;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Search;
using FurniSpace.Infrastructure.ReadModels.Products;
using static FurniSpace.Application.Constants.Search.ProductElasticsearchQueryFactoryConstants;

namespace FurniSpace.Application.Services.Search;

public static class ProductElasticsearchQueryFactory
{
    public const string CategoryFacetField = "categoryName.keyword";
    public const string MaterialFacetField = "material";
    public const string ColorFacetField = "color";

    private static readonly string[] ProductFacetFields =
    [
        CategoryFacetField,
        MaterialFacetField,
        ColorFacetField
    ];
    public static SearchRequest BuildSuggest(string query, int limit)
    {
        return new SearchRequest
        {
            AutocompleteText = query.Trim(),
            AutocompleteFields = ["productName.sayt"],
            Page = 1,
            PageSize = limit,
            Filters = CreatePublicProductFilters(),
            TrackTotalHits = false
        };
    }

    public static MoreLikeThisRequest BuildSimilar(int limit)
    {
        return new MoreLikeThisRequest
        {
            Fields = ["description", "material", "categoryName"],
            Filters = CreatePublicProductFilters(),
            Size = limit
        };
    }

    public static IReadOnlyList<SearchFilter> CreatePublicProductFilters()
    {
        return
        [
            new SearchFilter("isPublic", SearchFilterOperator.Term, true),
            new SearchFilter("status", SearchFilterOperator.Term, nameof(ProductStatus.ACTIVE))
        ];
    }

    public static ProductSearchRequestDto BuildSuggestFallbackQuery(string query, int limit)
    {
        return new ProductSearchRequestDto
        {
            Query = query,
            Page = 1,
            Limit = limit
        };
    }

    public static SearchRequest Build(ProductSearchRequestDto request)
    {
        var filters = new List<SearchFilter>(CreatePublicProductFilters());

        if (request.CategoryId.HasValue && request.CategoryId.Value != Guid.Empty)
        {
            filters.Add(new SearchFilter("categoryId", SearchFilterOperator.Term, request.CategoryId.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(request.Material))
        {
            filters.Add(new SearchFilter("material", SearchFilterOperator.Term, request.Material.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(request.Color))
        {
            filters.Add(new SearchFilter("color", SearchFilterOperator.Term, request.Color.Trim()));
        }

        if (request.MinPrice.HasValue)
        {
            filters.Add(new SearchFilter(EstimatedPriceField, SearchFilterOperator.RangeGte, request.MinPrice.Value));
        }

        if (request.MaxPrice.HasValue)
        {
            filters.Add(new SearchFilter(EstimatedPriceField, SearchFilterOperator.RangeLte, request.MaxPrice.Value));
        }

        return new SearchRequest
        {
            Query = BuildQueryString(request.Query),
            Page = request.Page,
            PageSize = request.Limit,
            Filters = filters,
            Sort = BuildSort(request.Sort),
            TrackTotalHits = true,
            FacetFields = ProductFacetFields
        };
    }

    public static ProductSearchQueryReadModel ToRepositoryQuery(ProductSearchRequestDto request)
    {
        return new ProductSearchQueryReadModel
        {
            Query = NormalizeOptional(request.Query),
            CategoryId = request.CategoryId,
            Material = NormalizeOptional(request.Material),
            Color = NormalizeOptional(request.Color),
            MinPrice = request.MinPrice,
            MaxPrice = request.MaxPrice,
            Sort = NormalizeOptional(request.Sort),
            Page = request.Page,
            Limit = request.Limit
        };
    }

    private static string? BuildQueryString(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var value = EscapeQueryString(query.Trim());
        return $"productName:*{value}* OR description:*{value}* OR categoryName:*{value}* OR material:*{value}* OR color:*{value}* OR productCode:*{value}*";
    }

    private static IReadOnlyList<SearchSortField> BuildSort(string? sort)
    {
        return sort?.Trim().ToLowerInvariant() switch
        {
            "price_asc" =>
            [
                new SearchSortField(EstimatedPriceField, SortDirection.Asc),
                new SearchSortField(ProductNameKeywordField, SortDirection.Asc)
            ],
            "price_desc" =>
            [
                new SearchSortField(EstimatedPriceField, SortDirection.Desc),
                new SearchSortField(ProductNameKeywordField, SortDirection.Asc)
            ],
            "created_asc" =>
            [
                new SearchSortField("createdAt", SortDirection.Asc),
                new SearchSortField(ProductNameKeywordField, SortDirection.Asc)
            ],
            _ =>
            [
                new SearchSortField("createdAt", SortDirection.Desc),
                new SearchSortField(ProductNameKeywordField, SortDirection.Asc)
            ]
        };
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

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
