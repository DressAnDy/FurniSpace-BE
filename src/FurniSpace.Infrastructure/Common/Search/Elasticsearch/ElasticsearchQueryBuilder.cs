using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using FurniSpace.Infrastructure.Common.Search;
using IndexSearchRequest = FurniSpace.Infrastructure.Common.Search.SearchRequest;

namespace FurniSpace.Infrastructure.Common.Search;

public static class ElasticsearchQueryBuilder
{
    public static void ApplySearchRequest<TDocument>(
        SearchRequestDescriptor<TDocument> descriptor,
        IndexSearchRequest request)
    {
        var from = Math.Max(0, (request.Page - 1) * request.PageSize);
        descriptor.From(from);
        descriptor.Size(request.PageSize);

        if (request.TrackTotalHits)
        {
            descriptor.TrackTotalHits(true);
        }

        ApplyQuery(descriptor, request);
        ApplySort(descriptor, request.Sort);

        if (request.FacetFields.Count > 0)
        {
            ElasticsearchAggregationHelper.ApplyTermsAggregations(descriptor, request.FacetFields, size: 50);
        }
    }

    internal static void ApplyAggregationRequest<TDocument>(
        SearchRequestDescriptor<TDocument> descriptor,
        SearchAggregationRequest request)
    {
        descriptor.Size(0);
        descriptor.TrackTotalHits(false);
        ApplyQuery(
            descriptor,
            new IndexSearchRequest
            {
                Query = request.Query,
                Filters = request.Filters
            });
    }

    private static void ApplyQuery<TDocument>(
        SearchRequestDescriptor<TDocument> descriptor,
        IndexSearchRequest request)
    {
        var hasQuery = !string.IsNullOrWhiteSpace(request.Query);
        var hasAutocomplete = !string.IsNullOrWhiteSpace(request.AutocompleteText) &&
            request.AutocompleteFields.Count > 0;
        var hasFilters = request.Filters.Count > 0 || request.FilterShouldMatchOne.Count > 0;

        if (!hasQuery && !hasAutocomplete && !hasFilters)
        {
            descriptor.Query(q => q.MatchAll(_ => { }));
            return;
        }

        descriptor.Query(q => q.Bool(b =>
        {
            if (hasAutocomplete)
            {
                var fields = ExpandAutocompleteFields(request.AutocompleteFields);
                b.Must(m => m.MultiMatch(mm => mm
                    .Query(request.AutocompleteText!)
                    .Type(TextQueryType.BoolPrefix)
                    .Fields(Fields.FromStrings(fields))));
            }
            else if (hasQuery)
            {
                b.Must(m => m.QueryString(qs => qs.Query(request.Query!)));
            }

            if (hasFilters)
            {
                b.Filter(BuildFilterQueries<TDocument>(request));
            }
        }));
    }

    private static string[] ExpandAutocompleteFields(IReadOnlyList<string> fields)
    {
        var expanded = new List<string>();

        foreach (var field in fields)
        {
            expanded.Add(field);
            expanded.Add($"{field}._2gram");
            expanded.Add($"{field}._3gram");
        }

        return expanded.ToArray();
    }

    private static Query[] BuildFilterQueries<TDocument>(IndexSearchRequest request)
    {
        var filters = new List<Query>(CreateFilterQueries<TDocument>(request.Filters));

        foreach (var group in request.FilterShouldMatchOne)
        {
            if (group.AnyOf.Count == 0)
            {
                continue;
            }

            filters.Add(new BoolQuery
            {
                Should = CreateFilterQueries<TDocument>(group.AnyOf),
                MinimumShouldMatch = 1
            });
        }

        return filters.ToArray();
    }

    internal static Query[] CreateFilterQueries<TDocument>(IReadOnlyList<SearchFilter> filters)
    {
        return filters
            .Select(filter => BuildFilterQuery<TDocument>(filter))
            .ToArray();
    }

    private static Query BuildFilterQuery<TDocument>(SearchFilter filter)
    {
        return filter.Operator switch
        {
            SearchFilterOperator.Term => new TermQuery
            {
                Field = filter.Field,
                Value = ConvertToFieldValue(filter.Value)
            },
            SearchFilterOperator.Terms => new TermsQuery
            {
                Field = filter.Field,
                Terms = new TermsQueryField(
                    (filter.Values ?? [])
                        .Select(ConvertToFieldValue)
                        .ToArray())
            },
            SearchFilterOperator.RangeGte => new NumberRangeQuery
            {
                Field = filter.Field,
                Gte = Convert.ToDouble(filter.Value)
            },
            SearchFilterOperator.RangeLte => new NumberRangeQuery
            {
                Field = filter.Field,
                Lte = Convert.ToDouble(filter.Value)
            },
            SearchFilterOperator.Exists => new ExistsQuery
            {
                Field = filter.Field
            },
            SearchFilterOperator.NotExists => new BoolQuery
            {
                MustNot = [new ExistsQuery { Field = filter.Field }]
            },
            _ => throw new InvalidOperationException($"Unsupported filter operator: {filter.Operator}")
        };
    }

    private static void ApplySort<TDocument>(
        SearchRequestDescriptor<TDocument> descriptor,
        IReadOnlyList<SearchSortField> sortFields)
    {
        if (sortFields.Count == 0)
        {
            return;
        }

        descriptor.Sort(sortDescriptor =>
        {
            foreach (var sortField in sortFields)
            {
                sortDescriptor.Field(
                    sortField.Field,
                    fieldSort => fieldSort.Order(
                        sortField.Direction == SortDirection.Desc ? SortOrder.Desc : SortOrder.Asc));
            }
        });
    }

    private static FieldValue ConvertToFieldValue(object? value)
    {
        return value switch
        {
            null => FieldValue.Null,
            string text => FieldValue.String(text),
            bool boolean => FieldValue.Boolean(boolean),
            int number => FieldValue.Long(number),
            long number => FieldValue.Long(number),
            double number => FieldValue.Double(number),
            decimal number => FieldValue.Double((double)number),
            Guid guid => FieldValue.String(guid.ToString()),
            DateTime dateTime => FieldValue.String(dateTime.ToString("O")),
            DateTimeOffset dateTimeOffset => FieldValue.String(dateTimeOffset.ToString("O")),
            _ => FieldValue.String(value.ToString() ?? string.Empty)
        };
    }
}
