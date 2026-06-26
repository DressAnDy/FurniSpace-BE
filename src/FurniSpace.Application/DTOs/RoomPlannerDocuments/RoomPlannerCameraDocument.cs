namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerCameraDocument
{
    public string Mode { get; set; } = "ORBIT";
    public RoomPlannerVector3Document Position { get; set; } = new();
    public RoomPlannerVector3Document Target { get; set; } = new();
    public decimal? Zoom { get; set; }
}
