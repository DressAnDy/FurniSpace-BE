#nullable enable

using System;
using System.Linq;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using FurniSpace.Infrastructure.Common.Search;
using Xunit;
using IndexSearchRequest = FurniSpace.Infrastructure.Common.Search.SearchRequest;

namespace FurniSpace.Infrastructure.Tests.Search;

public sealed class ElasticsearchQueryBuilderTests
{
    [Fact]
    public void ApplySearchRequest_WithCommonRequestOptions_DoesNotThrow()
    {
        var descriptor = new SearchRequestDescriptor<TestDocument>();

        ElasticsearchQueryBuilder.ApplySearchRequest(
            descriptor,
            new IndexSearchRequest
            {
                Query = "chair AND table",
                Page = 3,
                PageSize = 15,
                TrackTotalHits = true,
                Filters = [new("status", SearchFilterOperator.Term, "ACTIVE")],
                Sort = [new("createdAt", SortDirection.Desc)],
                FacetFields = ["status", "category.name"]
            });
    }

    [Fact]
    public void ApplySearchRequest_WithAutocompleteAndShouldGroup_DoesNotThrow()
    {
        var descriptor = new SearchRequestDescriptor<TestDocument>();

        ElasticsearchQueryBuilder.ApplySearchRequest(
            descriptor,
            new IndexSearchRequest
            {
                AutocompleteText = "so",
                AutocompleteFields = ["name", "sku"],
                FilterShouldMatchOne =
                [
                    new([
                        new("categoryId", SearchFilterOperator.Term, 10),
                        new("categoryId", SearchFilterOperator.Term, 20L)
                    ]),
                    new([])
                ]
            });
    }

    [Fact]
    public void ApplyAggregationRequest_WithQueryAndFilters_DoesNotThrow()
    {
        var descriptor = new SearchRequestDescriptor<TestDocument>();

        ElasticsearchQueryBuilder.ApplyAggregationRequest(
            descriptor,
            new SearchAggregationRequest
            {
                Query = "lamp",
                Filters = [new("status", SearchFilterOperator.Term, "ACTIVE")]
            });
    }

    [Fact]
    public void CreateFilterQueries_BuildsAllSupportedFilterTypes()
    {
        var accountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var publishedAt = new DateTime(2026, 6, 28, 8, 30, 0, DateTimeKind.Utc);

        var queries = ElasticsearchQueryBuilder.CreateFilterQueries(
        [
            new("status", SearchFilterOperator.Term, "ACTIVE"),
            new("tags", SearchFilterOperator.Terms, Values: ["wood", "modern"]),
            new("price", SearchFilterOperator.RangeGte, 100m),
            new("rating", SearchFilterOperator.RangeLte, 4.5),
            new("metadata", SearchFilterOperator.Exists),
            new("archivedAt", SearchFilterOperator.NotExists),
            new("ownerId", SearchFilterOperator.Term, accountId),
            new("publishedAt", SearchFilterOperator.Term, publishedAt),
            new("isFeatured", SearchFilterOperator.Term, true),
            new("missing", SearchFilterOperator.Term)
        ]);

        Assert.Equal("status", queries[0].Term!.Field!.ToString());
        Assert.Equal("ACTIVE", queries[0].Term!.Value.ToString());

        Assert.Equal("tags", queries[1].Terms!.Field!.ToString());
        Assert.NotNull(queries[1].Terms!.Terms);

        var priceRange = Assert.IsType<NumberRangeQuery>(queries[2].Range);
        Assert.Equal("price", priceRange.Field!.ToString());
        Assert.NotNull(priceRange.Gte);

        var ratingRange = Assert.IsType<NumberRangeQuery>(queries[3].Range);
        Assert.Equal("rating", ratingRange.Field!.ToString());
        Assert.NotNull(ratingRange.Lte);

        Assert.Equal("metadata", queries[4].Exists!.Field!.ToString());
        Assert.Equal("archivedAt", queries[5].Bool!.MustNot!.First().Exists!.Field!.ToString());
        Assert.Equal(accountId.ToString(), queries[6].Term!.Value.ToString());
        Assert.Equal("2026-06-28T08:30:00.0000000Z", queries[7].Term!.Value.ToString());
        Assert.Equal("True", queries[8].Term!.Value.ToString());
        Assert.Equal("null", queries[9].Term!.Value.ToString());
    }

    [Fact]
    public void CreateFilterQueries_WithUnsupportedOperator_Throws()
    {
        var unsupported = (SearchFilterOperator)999;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ElasticsearchQueryBuilder.CreateFilterQueries(
            [
                new("status", unsupported, "ACTIVE")
            ]));

        Assert.Contains("Unsupported filter operator", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("category.name", "category_name")]
    [InlineData("status", "status")]
    public void ToAggregationName_ReplacesDots(string field, string expected)
    {
        Assert.Equal(expected, ElasticsearchAggregationHelper.ToAggregationName(field));
    }

    private sealed class TestDocument;
}
