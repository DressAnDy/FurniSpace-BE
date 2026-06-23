using System;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Infrastructure.Persistence;

namespace FurniSpace.Application.Tests.TestDoubles;

internal sealed class TestUnitOfWork : IUnitOfWork
{
    private readonly Func<CancellationToken, Task<int>> _saveChanges;

    public static readonly IUnitOfWork Instance = new TestUnitOfWork(_ => Task.FromResult(0));

    private TestUnitOfWork(Func<CancellationToken, Task<int>> saveChanges)
    {
        _saveChanges = saveChanges;
    }

    public static IUnitOfWork ForSaveChanges(Func<CancellationToken, Task<int>> saveChanges)
    {
        ArgumentNullException.ThrowIfNull(saveChanges);
        return new TestUnitOfWork(saveChanges);
    }

    public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _saveChanges(cancellationToken);
    }

    public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public static IUnitOfWork ForFailingSaveChanges()
    {
        return new TestUnitOfWork(_ => throw new InvalidOperationException("Save failed."));
    }
}
