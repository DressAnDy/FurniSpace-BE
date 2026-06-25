namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerOpeningDocument
{
    public string OpeningId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string WallId { get; set; } = string.Empty;
    public decimal? OffsetFromWallStart { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public string? SwingDirection { get; set; }
    public string? Orientation { get; set; }
    public decimal? SillHeight { get; set; }
}
