using FurniSpace.Application.DTOs.RoomPlannerDocuments;

namespace FurniSpace.Application.Interfaces.RoomPlanner;

public interface IRoomPlannerSceneRepository
{
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
