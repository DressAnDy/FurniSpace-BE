namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerFloorOpeningDocument
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Label { get; set; }
    public RoomPlannerPoint2Document? Position { get; set; }
    public decimal? Width { get; set; }
    public decimal? Depth { get; set; }
    public Guid? LayoutAssetId { get; set; }
    public RoomPlannerModelSnapshotDocument? ModelSnapshot { get; set; }
}
