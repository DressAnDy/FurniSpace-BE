namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerVisualSnapshotDocument
{
    public Guid? ThumbnailFileId { get; set; }
    public string? ThumbnailUrlSnapshot { get; set; }
    public string? Material { get; set; }
    public string? Color { get; set; }
    public string? Finish { get; set; }
}
