using FurniSpace.Shared.DTOs.RoomPlanner;

namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerBlueprintLayoutDocument
    : RoomPlannerBlueprintLayoutBase<RoomPlannerPoint2Document, RoomPlannerBlueprintFloorDocument>
{
    public RoomPlannerBlueprintFloorDocument? FindFloor(string floorId) =>
        Floors.FirstOrDefault(floor => string.Equals(floor.Id, floorId, StringComparison.OrdinalIgnoreCase));
}
