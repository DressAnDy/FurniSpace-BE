using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.DTOs.Search;
using FurniSpace.Infrastructure.Common.Search;

namespace FurniSpace.Application.Services.Search;

public static class SearchFacetMapper
{
    public static IReadOnlyList<SearchFacetItemDto> ToDto(IReadOnlyList<SearchFacetBucket> buckets)
    {
        return buckets
            .Select(bucket => new SearchFacetItemDto
            {
                Key = bucket.Key,
                Count = bucket.Count
            })
            .ToList();
    }

    public static ProductSearchFacetsDto ToProductFacets(
        IReadOnlyDictionary<string, IReadOnlyList<SearchFacetBucket>> facets)
    {
        return new ProductSearchFacetsDto
        {
            Categories = ToDto(GetBuckets(facets, ProductElasticsearchQueryFactory.CategoryFacetField)),
            Materials = ToDto(GetBuckets(facets, ProductElasticsearchQueryFactory.MaterialFacetField)),
            Colors = ToDto(GetBuckets(facets, ProductElasticsearchQueryFactory.ColorFacetField))
        };
    }

    private static IReadOnlyList<SearchFacetBucket> GetBuckets(
        IReadOnlyDictionary<string, IReadOnlyList<SearchFacetBucket>> facets,
        string field)
    {
        return facets.TryGetValue(field, out var buckets) ? buckets : [];
    }
}
