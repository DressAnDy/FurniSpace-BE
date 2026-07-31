namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerBlueprintFloorDocument
{
    public string Id { get; set; } = string.Empty;
    public Guid ProjectAreaId { get; set; }
    public string? Name { get; set; }
    public int? LevelIndex { get; set; }
    public decimal? Elevation { get; set; }
    public decimal? FloorHeight { get; set; }
    public decimal? SlabThickness { get; set; }
    public List<RoomPlannerPoint2Document> Points { get; set; } = [];
    public List<Dictionary<string, object?>> Rooms { get; set; } = [];
    public List<RoomPlannerWallDocument> Walls { get; set; } = [];
    public List<RoomPlannerOpeningDocument> Doors { get; set; } = [];
    public List<RoomPlannerOpeningDocument> Windows { get; set; } = [];
    public List<RoomPlannerOpeningDocument> Openings { get; set; } = [];
    public List<Dictionary<string, object?>> Slabs { get; set; } = [];
    public List<Dictionary<string, object?>> Stairs { get; set; } = [];
    public List<Dictionary<string, object?>> Balconies { get; set; } = [];
    public List<Dictionary<string, object?>> Yards { get; set; } = [];
    public List<Dictionary<string, object?>> Columns { get; set; } = [];
    public List<Dictionary<string, object?>> Beams { get; set; } = [];
}
