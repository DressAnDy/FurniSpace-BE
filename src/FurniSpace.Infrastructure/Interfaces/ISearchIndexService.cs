namespace FurniSpace.Infrastructure.Interfaces;

public interface ISearchIndexService
{
    Task IndexAsync<TDocument>(
        string indexName,
        string id,
        TDocument document,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string indexName,
        string id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TDocument>> SearchAsync<TDocument>(
        string indexName,
        string query,
        int size = 100,
        CancellationToken cancellationToken = default);
}
