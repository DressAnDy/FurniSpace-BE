namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerDimensionsSnapshotDocument
{
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public decimal? Depth { get; set; }
    public string Unit { get; set; } = "cm";
}
