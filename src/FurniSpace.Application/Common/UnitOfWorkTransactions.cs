using FurniSpace.Infrastructure.Persistence;

namespace FurniSpace.Application.Common;

internal static class UnitOfWorkTransactions
{
    public static async Task<T> ExecuteAsync<T>(
        IUnitOfWork unitOfWork,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await action(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            return result;
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public static async Task ExecuteAsync(
        IUnitOfWork unitOfWork,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await action(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
