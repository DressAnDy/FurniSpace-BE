using FurniSpace.Infrastructure.Mongo;
using FurniSpace.Infrastructure.Repositories.IRepository;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class RoomPlannerSceneRepository : IRoomPlannerSceneRepository
{
    private readonly IRoomPlannerSceneCollection _scenes;

    public RoomPlannerSceneRepository(IRoomPlannerSceneCollection scenes)
    {
        _scenes = scenes;
    }

    public Task EnsureIndexesAsync(CancellationToken cancellationToken = default) =>
        ExecuteMongoOperationAsync(
            () => _scenes.EnsureIndexesAsync(cancellationToken),
            RoomPlannerMongoErrorCodes.OperationFailed);

    public Task<RoomPlannerSceneDocument> UpsertBySqlSceneIdAsync(
        RoomPlannerSceneDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        return ExecuteMongoOperationAsync(
            () => SaveCoreAsync(document, cancellationToken),
            RoomPlannerMongoErrorCodes.OperationFailed);
    }

    public Task<RoomPlannerSceneDocument?> GetByIdAsync(
        string mongoSceneId,
        CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(mongoSceneId, out _))
        {
            return Task.FromResult<RoomPlannerSceneDocument?>(null);
        }

        return ExecuteMongoOperationAsync(
            () => _scenes.FindByMongoIdAsync(mongoSceneId, cancellationToken),
            RoomPlannerMongoErrorCodes.OperationFailed);
    }

    public Task<RoomPlannerSceneDocument?> GetBySqlSceneIdAsync(
        Guid sqlSceneId,
        CancellationToken cancellationToken = default) =>
        ExecuteMongoOperationAsync(
            () => _scenes.FindBySqlSceneIdAsync(sqlSceneId, cancellationToken),
            RoomPlannerMongoErrorCodes.OperationFailed);

    public Task<bool> DeleteBySqlSceneIdAsync(
        Guid sqlSceneId,
        CancellationToken cancellationToken = default) =>
        ExecuteMongoOperationAsync(
            () => _scenes.DeleteBySqlSceneIdAsync(sqlSceneId, cancellationToken),
            RoomPlannerMongoErrorCodes.OperationFailed);

    private async Task<RoomPlannerSceneDocument> SaveCoreAsync(
        RoomPlannerSceneDocument scene,
        CancellationToken cancellationToken)
    {
        scene.Metadata ??= new RoomPlannerSceneMetadataDocument();
        scene.Metadata.UpdatedAt ??= DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(scene.Id))
        {
            if (!ObjectId.TryParse(scene.Id, out _))
            {
                throw new MongoRoomPlannerException(
                    RoomPlannerMongoErrorCodes.OperationFailed,
                    "Mongo scene id is not a valid ObjectId.");
            }

            return await _scenes.UpsertByMongoIdAsync(scene, cancellationToken).ConfigureAwait(false);
        }

        var existingScene = await _scenes.FindBySqlSceneIdAsync(scene.SqlSceneId, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(existingScene?.Id))
        {
            scene.Id = existingScene.Id;
        }
        else
        {
            scene.Id = ObjectId.GenerateNewId().ToString();
        }

        return await _scenes.UpsertBySqlSceneIdAsync(scene, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T> ExecuteMongoOperationAsync<T>(
        Func<Task<T>> operation,
        string fallbackErrorCode)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (MongoRoomPlannerException)
        {
            throw;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new MongoRoomPlannerException(
                RoomPlannerMongoErrorCodes.DuplicateSqlSceneId,
                "A Room Planner scene already exists for this SQL scene id.",
                exception);
        }
        catch (MongoException exception)
        {
            throw new MongoRoomPlannerException(
                fallbackErrorCode,
                "MongoDB operation failed.",
                exception);
        }
    }

    private static async Task ExecuteMongoOperationAsync(
        Func<Task> operation,
        string fallbackErrorCode)
    {
        await ExecuteMongoOperationAsync(
            async () =>
            {
                await operation().ConfigureAwait(false);
                return true;
            },
            fallbackErrorCode).ConfigureAwait(false);
    }
}
