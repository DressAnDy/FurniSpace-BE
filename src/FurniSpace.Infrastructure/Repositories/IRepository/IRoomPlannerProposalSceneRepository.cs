using FurniSpace.Infrastructure.DTOs.RoomPlanner;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IRoomPlannerProposalSceneRepository
{
    Task<RoomPlannerSceneContextReadModel?> GetContextAsync(
        Guid sceneId,
        CancellationToken cancellationToken = default);

    Task UpdateMongoSceneIdAsync(
        Guid sceneId,
        string mongoSceneId,
        CancellationToken cancellationToken = default);
}
