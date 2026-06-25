namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerLayoutDocument
{
    public string Type { get; set; } = "WALL_BOUNDARY";
    public bool IsClosed { get; set; }
    public decimal? AreaSqm { get; set; }
    public decimal? DefaultWallHeight { get; set; }
    public decimal? DefaultWallThickness { get; set; }
    public List<RoomPlannerPoint2Document> Boundary { get; set; } = [];
    public List<RoomPlannerWallDocument> Walls { get; set; } = [];
    public List<RoomPlannerOpeningDocument> Openings { get; set; } = [];
    public RoomPlannerFloorDocument Floor { get; set; } = new();
}
