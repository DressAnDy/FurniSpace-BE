namespace FurniSpace.Infrastructure.Repositories.Base;

public interface IGenericRepository<TEntity>
    where TEntity : class
{
    IQueryable<TEntity> Query();

    /// <summary>
    /// Explicitly opens the underlying database connection if it is not already open.
    /// Exposed for latency diagnostics so callers can time connection establishment
    /// (TCP/TLS handshake, remote compute wake-up) separately from query execution.
    /// Default no-op keeps in-memory/fake test repositories compiling without changes.
    /// </summary>
    Task EnsureConnectionOpenAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken = default);
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
    void Update(TEntity entity);
    void Remove(TEntity entity);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
