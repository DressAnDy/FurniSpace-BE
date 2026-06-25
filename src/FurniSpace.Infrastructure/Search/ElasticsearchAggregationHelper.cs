using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using FurniSpace.Infrastructure.Common.Search;

namespace FurniSpace.Infrastructure.Search;

internal static class ElasticsearchAggregationHelper
{
    public static void ApplyTermsAggregations<TDocument>(
        SearchRequestDescriptor<TDocument> descriptor,
        IReadOnlyList<string> fields,
        int size)
    {
        if (fields.Count == 0)
        {
            return;
        }

        descriptor.Aggregations(aggregations =>
        {
            foreach (var field in fields)
            {
                var aggregationName = ToAggregationName(field);
                aggregations.Add(
                    aggregationName,
                    aggregation => aggregation.Terms(terms => terms.Field(field).Size(size)));
            }
        });
    }

    public static IReadOnlyDictionary<string, IReadOnlyList<SearchFacetBucket>> ParseTermsAggregations(
        AggregateDictionary? aggregations,
        IReadOnlyList<string> fields)
    {
        if (aggregations is null || fields.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<SearchFacetBucket>>();
        }

        var facets = new Dictionary<string, IReadOnlyList<SearchFacetBucket>>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            var aggregationName = ToAggregationName(field);
            var terms = aggregations.GetStringTerms(aggregationName);
            if (terms is null)
            {
                facets[field] = [];
                continue;
            }

            facets[field] = terms.Buckets
                .Where(bucket => !string.IsNullOrWhiteSpace(bucket.Key.ToString()))
                .Select(bucket => new SearchFacetBucket
                {
                    Key = bucket.Key.ToString() ?? string.Empty,
                    Count = bucket.DocCount
                })
                .OrderByDescending(bucket => bucket.Count)
                .ThenBy(bucket => bucket.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return facets;
    }

    internal static string ToAggregationName(string field)
        => field.Replace(".", "_", StringComparison.Ordinal);
}
