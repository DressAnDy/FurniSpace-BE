namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerPlacementDocument
{
    public string Mode { get; set; } = "FLOOR";
    public decimal? HeightOffset { get; set; }
    public string? SupportObjectId { get; set; }
    public string? MountedWallId { get; set; }
}
