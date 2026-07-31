using FurniSpace.Shared.DTOs.RoomPlanner;

namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerBlueprintFloorDocument
    : RoomPlannerBlueprintFloorBase<RoomPlannerPoint2Document, RoomPlannerWallDocument>
{
    public bool ContainsWall(string wallId) =>
        Walls.Any(wall => string.Equals(wall.WallId, wallId, StringComparison.OrdinalIgnoreCase));
}
