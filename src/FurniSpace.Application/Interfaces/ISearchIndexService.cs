namespace FurniSpace.Application.Interfaces;

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
        CancellationToken cancellationToken = default);
}
