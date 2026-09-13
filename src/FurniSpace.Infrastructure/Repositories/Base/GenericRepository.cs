using FurniSpace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Base;

public class GenericRepository<TEntity> : IGenericRepository<TEntity>
    where TEntity : class
{
    protected GenericRepository(AppDbContext dbContext)
    {
        DbContext = dbContext;
        DbSet = dbContext.Set<TEntity>();
    }

    protected AppDbContext DbContext { get; }
    protected DbSet<TEntity> DbSet { get; }

    public IQueryable<TEntity> Query()
    {
        // Read-only query surface: callers use this for list/count/exists projections,
        // never for fetching an entity to mutate and save, so tracking is unnecessary overhead.
        return DbSet.AsNoTracking();
    }

    public async Task EnsureConnectionOpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = DbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await DbContext.Database.OpenConnectionAsync(cancellationToken);
        }
    }

    public async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet.FindAsync([id], cancellationToken);
    }

    public async Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await DbSet.AddAsync(entity, cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        await DbSet.AddRangeAsync(entities, cancellationToken);
    }

    public void Update(TEntity entity)
    {
        DbSet.Update(entity);
    }

    public void Remove(TEntity entity)
    {
        DbSet.Remove(entity);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return DbContext.SaveChangesAsync(cancellationToken);
    }
}
