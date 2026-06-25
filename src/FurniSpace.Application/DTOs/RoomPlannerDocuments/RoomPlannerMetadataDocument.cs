namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerMetadataDocument
{
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
