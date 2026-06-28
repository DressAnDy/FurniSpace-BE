#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using FurniSpace.Infrastructure.Common.Search;
using Microsoft.Extensions.Options;
using Xunit;
using IndexSearchRequest = FurniSpace.Infrastructure.Common.Search.SearchRequest;

namespace FurniSpace.Infrastructure.Tests.Search;

public sealed class ElasticsearchIndexManagerAndServiceTests
{
    [Fact]
    public async Task IndexManager_WhenIndexExists_DoesNotCreateIndex()
    {
        var manager = new ElasticsearchIndexManager(CreateClient(statusCode: 200, "{}"), Options.Create(Settings()));

        var exists = await manager.IndexExistsAsync("products");
        await manager.EnsureIndexAsync("products");

        Assert.True(exists);
    }

    [Fact]
    public async Task IndexManager_WhenMappingIsMissing_Throws()
    {
        var manager = new ElasticsearchIndexManager(CreateClient(statusCode: 404, "{}"), Options.Create(Settings()));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.EnsureIndexAsync("unknown-index"));

        Assert.Contains("No Elasticsearch mapping is registered", exception.Message);
    }

    [Fact]
    public async Task IndexManager_DeleteIndex_AllowsMissingIndex()
    {
        var manager = new ElasticsearchIndexManager(CreateClient(statusCode: 404, "{}"), Options.Create(Settings()));

        await manager.DeleteIndexAsync("products");
    }

    [Fact]
    public async Task IndexService_IndexBulkDeleteAndSuggest_UseConfiguredIndexNames()
    {
        var service = new ElasticsearchIndexService(CreateClient(statusCode: 200, "{}"), Options.Create(Settings()));

        await service.IndexAsync("products", "1", new TestDocument { Name = "Chair" });
        await service.BulkIndexAsync(
            "products",
            [
                new BulkIndexItem<TestDocument>("1", new TestDocument { Name = "Chair" }),
                new BulkIndexItem<TestDocument>("2", new TestDocument { Name = "Table" })
            ]);
        await service.DeleteAsync("products", "1");
        var suggest = await service.SuggestAsync("products", new SuggestRequest { Text = "ch", Field = "name" });

        Assert.Empty(suggest.Suggestions);
    }

    [Fact]
    public async Task IndexService_DeleteAsync_AllowsMissingDocument()
    {
        var service = new ElasticsearchIndexService(CreateClient(statusCode: 404, "{}"), Options.Create(Settings()));

        await service.DeleteAsync("products", "missing");
    }

    [Fact]
    public async Task IndexService_IndexAsync_WhenResponseInvalid_Throws()
    {
        var service = new ElasticsearchIndexService(CreateClient(statusCode: 500, "{}"), Options.Create(Settings()));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.IndexAsync("products", "1", new TestDocument { Name = "Chair" }));
    }

    [Fact]
    public async Task IndexService_SearchMoreLikeThisAndAggregate_MapSuccessfulResponses()
    {
        var service = new ElasticsearchIndexService(CreateClient(statusCode: 200, SearchBody), Options.Create(Settings()));

        var search = await service.SearchAsync<TestDocument>(
            "products",
            new IndexSearchRequest
            {
                Query = "chair",
                Page = 2,
                PageSize = 5,
                FacetFields = ["status"]
            });
        var simple = await service.SearchAsync<TestDocument>("products", "chair", size: 3);
        var moreLikeThis = await service.MoreLikeThisAsync<TestDocument>(
            "products",
            "1",
            new MoreLikeThisRequest { Size = 4, Fields = ["name"] });
        var aggregate = await service.AggregateAsync(
            "products",
            new SearchAggregationRequest { TermsFields = ["status"], TermsSize = 5 });

        Assert.Equal(1, search.Total);
        Assert.Equal(2, search.Page);
        Assert.Equal(5, search.PageSize);
        Assert.Equal("Chair", search.Documents[0].Name);
        Assert.Single(simple);
        Assert.Equal(1, moreLikeThis.Total);
        Assert.Equal(4, moreLikeThis.PageSize);
        Assert.True(aggregate.Facets.ContainsKey("status"));
    }

    private static ElasticsearchClient CreateClient(int statusCode, string body)
    {
        var invoker = new InMemoryRequestInvoker(
            Encoding.UTF8.GetBytes(body),
            statusCode,
            exception: null!,
            contentType: "application/json",
            headers: new Dictionary<string, IEnumerable<string>>
            {
                ["x-elastic-product"] = ["Elasticsearch"]
            });
        return new ElasticsearchClient(new ElasticsearchClientSettings(invoker).DefaultIndex("test-default"));
    }

    private static ElasticsearchSettings Settings()
        => new()
        {
            IndexPrefix = "test"
        };

    private sealed class TestDocument
    {
        public string Name { get; set; } = string.Empty;
    }

    private const string SearchBody = """
        {
          "took": 1,
          "timed_out": false,
          "hits": {
            "total": { "value": 1, "relation": "eq" },
            "max_score": 1,
            "hits": [
              { "_index": "test-products", "_id": "1", "_score": 1, "_source": { "name": "Chair" } }
            ]
          },
          "aggregations": {
            "sterms#status": {
              "buckets": [
                { "key": "ACTIVE", "doc_count": 1 }
              ]
            }
          }
        }
        """;
}
