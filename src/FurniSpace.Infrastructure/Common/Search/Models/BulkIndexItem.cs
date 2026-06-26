namespace FurniSpace.Infrastructure.Common.Search;

public sealed record BulkIndexItem<TDocument>(string Id, TDocument Document);
