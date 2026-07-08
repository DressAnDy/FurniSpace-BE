namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerModelSnapshotDocument
{
    public Guid? ModelFileId { get; set; }
    public string? Format { get; set; }
    public string? ModelUrlSnapshot { get; set; }
}
