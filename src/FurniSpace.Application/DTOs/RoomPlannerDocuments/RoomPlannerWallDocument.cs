namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerWallDocument
{
    public string WallId { get; set; } = string.Empty;
    public RoomPlannerPoint2Document Start { get; set; } = new();
    public RoomPlannerPoint2Document End { get; set; } = new();
    public decimal? Height { get; set; }
    public decimal? Thickness { get; set; }
    public bool Visible { get; set; } = true;
    public bool Locked { get; set; }
    public RoomPlannerStyleDocument Style { get; set; } = new();
}
