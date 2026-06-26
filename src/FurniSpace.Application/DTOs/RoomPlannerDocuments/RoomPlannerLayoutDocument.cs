using FurniSpace.Shared.DTOs.RoomPlanner;

namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerLayoutDocument
    : RoomPlannerLayoutBase<
        RoomPlannerPoint2Document,
        RoomPlannerWallDocument,
        RoomPlannerOpeningDocument,
        RoomPlannerFloorDocument>
{
    public RoomPlannerLayoutDocument()
    {
        Floor = new RoomPlannerFloorDocument();
    }
}
