using FurniSpace.Shared.DTOs.RoomPlanner;

namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerWallDocument
    : RoomPlannerWallBase<RoomPlannerPoint2Document, RoomPlannerStyleDocument>
{
    public RoomPlannerWallDocument()
    {
        Start = new RoomPlannerPoint2Document();
        End = new RoomPlannerPoint2Document();
        Style = new RoomPlannerStyleDocument();
    }
}
