namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerObjectDocument
{
    public string ObjectId { get; set; } = string.Empty;
    public Guid? ProposalItemId { get; set; }
    public Guid ProductVersionId { get; set; }
    public string ObjectType { get; set; } = "FURNITURE";
    public string? Name { get; set; }
    public RoomPlannerTransformDocument Transform { get; set; } = new();
    public RoomPlannerDimensionsSnapshotDocument DimensionsSnapshot { get; set; } = new();
    public RoomPlannerVisualSnapshotDocument? VisualSnapshot { get; set; }
    public RoomPlannerModelSnapshotDocument? ModelSnapshot { get; set; }
    public Dictionary<string, object?> MaterialOverrides { get; set; } = [];
    public bool Visible { get; set; } = true;
    public bool Locked { get; set; }
}
