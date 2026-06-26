using FurniSpace.Infrastructure.Mongo;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IRoomPlannerSceneRepository
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);

    Task<RoomPlannerSceneDocument?> GetByIdAsync(
        string mongoSceneId,
        CancellationToken cancellationToken = default);

    Task<RoomPlannerSceneDocument?> GetBySqlSceneIdAsync(
        Guid sqlSceneId,
        CancellationToken cancellationToken = default);

    Task<RoomPlannerSceneDocument> UpsertBySqlSceneIdAsync(
        RoomPlannerSceneDocument document,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteBySqlSceneIdAsync(
        Guid sqlSceneId,
        CancellationToken cancellationToken = default);
}
