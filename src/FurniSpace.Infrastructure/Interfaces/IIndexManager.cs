namespace FurniSpace.Infrastructure.Interfaces;

public interface IIndexManager
{
    Task EnsureIndexAsync(string indexName, CancellationToken cancellationToken = default);

    Task<bool> IndexExistsAsync(string indexName, CancellationToken cancellationToken = default);

    Task DeleteIndexAsync(string indexName, CancellationToken cancellationToken = default);
}
