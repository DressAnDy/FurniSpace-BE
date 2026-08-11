using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.RoomPlanner;

namespace FurniSpace.Application.Interfaces.RoomPlanner;

public interface IRoomPlannerSceneService
{
    Task<ServiceResult<RoomPlannerSceneSaveResponseDto>> SaveSceneAsync(
        Guid sceneId,
        RoomPlannerScenePayloadDto request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<RoomPlannerSceneResponseDto>> GetSceneAsync(
        Guid sceneId,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResolveRoomPlannerProductsResponseDto>> ResolveProductsAsync(
        Guid sceneId,
        ResolveRoomPlannerProductsRequestDto request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
