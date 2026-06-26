using FurniSpace.Shared.DTOs.RoomPlanner;

namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerObjectDocument
    : RoomPlannerObjectBase<
        RoomPlannerTransformDocument,
        RoomPlannerDimensionsSnapshotDocument,
        RoomPlannerVisualSnapshotDocument,
        RoomPlannerModelSnapshotDocument>
{
    public RoomPlannerObjectDocument()
    {
        Transform = new RoomPlannerTransformDocument();
        DimensionsSnapshot = new RoomPlannerDimensionsSnapshotDocument();
    }
}
