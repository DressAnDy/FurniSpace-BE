namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerBlueprintLayoutDocument
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string Unit { get; set; } = "meter";
    public decimal? Scale { get; set; }
    public RoomPlannerPoint2Document? Origin { get; set; }
    public decimal? NorthDirection { get; set; }
    public List<RoomPlannerBlueprintFloorDocument> Floors { get; set; } = [];
    public Dictionary<string, object?> Metadata { get; set; } = [];
}
