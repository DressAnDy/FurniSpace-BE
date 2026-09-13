using FurniSpace.Shared.DTOs.RoomPlanner;

namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerSceneLinksDocument : RoomPlannerSceneLinksBase
{
    public bool ContainsProjectArea(Guid projectAreaId) => ProjectAreaIds.Contains(projectAreaId);
}
