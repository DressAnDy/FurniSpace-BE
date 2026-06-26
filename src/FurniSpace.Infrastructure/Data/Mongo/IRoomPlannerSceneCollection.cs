namespace FurniSpace.Infrastructure.Data.Mongo;

public interface IRoomPlannerSceneCollection
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);

    Task<RoomPlannerSceneDocument?> FindByMongoIdAsync(
        string mongoSceneId,
        CancellationToken cancellationToken = default);

    Task<RoomPlannerSceneDocument?> FindBySqlSceneIdAsync(
        Guid sqlSceneId,
        CancellationToken cancellationToken = default);

    Task<RoomPlannerSceneDocument> UpsertByMongoIdAsync(
        RoomPlannerSceneDocument document,
        CancellationToken cancellationToken = default);

    Task<RoomPlannerSceneDocument> UpsertBySqlSceneIdAsync(
        RoomPlannerSceneDocument document,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteBySqlSceneIdAsync(
        Guid sqlSceneId,
        CancellationToken cancellationToken = default);
}
