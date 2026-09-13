using System;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Infrastructure.Persistence;

namespace FurniSpace.Application.Tests.TestDoubles;

internal sealed class TestUnitOfWork : IUnitOfWork
{
    private readonly Func<CancellationToken, Task<int>> _saveChanges;
    private readonly Func<CancellationToken, Task> _beginTransaction;
    private readonly Func<CancellationToken, Task> _commitTransaction;
    private readonly Func<CancellationToken, Task> _rollbackTransaction;

    public static readonly IUnitOfWork Instance = new TestUnitOfWork(
        _ => Task.CompletedTask,
        _ => Task.FromResult(0),
        _ => Task.CompletedTask,
        _ => Task.CompletedTask);

    private TestUnitOfWork(
        Func<CancellationToken, Task> beginTransaction,
        Func<CancellationToken, Task<int>> saveChanges,
        Func<CancellationToken, Task> commitTransaction,
        Func<CancellationToken, Task> rollbackTransaction)
    {
        _beginTransaction = beginTransaction;
        _saveChanges = saveChanges;
        _commitTransaction = commitTransaction;
        _rollbackTransaction = rollbackTransaction;
    }

    public static IUnitOfWork ForSaveChanges(Func<CancellationToken, Task<int>> saveChanges)
    {
        ArgumentNullException.ThrowIfNull(saveChanges);
        return new TestUnitOfWork(
            _ => Task.CompletedTask,
            saveChanges,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask);
    }

    public static IUnitOfWork ForTransaction(
        Func<CancellationToken, Task> beginTransaction,
        Func<CancellationToken, Task<int>> saveChanges,
        Func<CancellationToken, Task> commitTransaction,
        Func<CancellationToken, Task> rollbackTransaction)
    {
        ArgumentNullException.ThrowIfNull(beginTransaction);
        ArgumentNullException.ThrowIfNull(saveChanges);
        ArgumentNullException.ThrowIfNull(commitTransaction);
        ArgumentNullException.ThrowIfNull(rollbackTransaction);
        return new TestUnitOfWork(
            beginTransaction,
            saveChanges,
            commitTransaction,
            rollbackTransaction);
    }

    public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return _beginTransaction(cancellationToken);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _saveChanges(cancellationToken);
    }

    public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        return _commitTransaction(cancellationToken);
    }

    public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        return _rollbackTransaction(cancellationToken);
    }

    public static IUnitOfWork ForFailingSaveChanges()
    {
        return new TestUnitOfWork(
            _ => Task.CompletedTask,
            _ => throw new InvalidOperationException("Save failed."),
            _ => Task.CompletedTask,
            _ => Task.CompletedTask);
    }
}
