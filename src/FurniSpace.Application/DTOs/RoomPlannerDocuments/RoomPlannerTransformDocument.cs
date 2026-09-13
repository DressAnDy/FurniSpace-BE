namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerTransformDocument
{
    public RoomPlannerVector3Document Position { get; set; } = new();
    public RoomPlannerVector3Document Rotation { get; set; } = new();
    public RoomPlannerVector3Document Scale { get; set; } = new() { X = 1, Y = 1, Z = 1 };
}
