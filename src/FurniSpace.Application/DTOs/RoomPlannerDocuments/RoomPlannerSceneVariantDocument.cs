namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerSceneVariantDocument
{
    public string? Id { get; set; }
    public int SchemaVersion { get; set; } = 2;
    public Guid VariantId { get; set; }
    public string BaseMongoSceneId { get; set; } = string.Empty;
    public Guid ProposalId { get; set; }
    public Guid SceneId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProjectAreaId { get; set; }
    public string SceneKind { get; set; } = "CUSTOMER_SUGGESTION";
    public string Status { get; set; } = "DRAFT";
    public Guid CreatedBy { get; set; }
    public Guid? ReviewedBy { get; set; }
    public Guid? AppliedBy { get; set; }
    public RoomPlannerLayoutDocument Layout { get; set; } = new();
    public List<RoomPlannerObjectDocument> Objects { get; set; } = [];
    public RoomPlannerCameraDocument Camera { get; set; } = new();
    public RoomPlannerLightingDocument Lighting { get; set; } = new();
    public RoomPlannerValidationDocument Validation { get; set; } = new();
    public string? CustomerNote { get; set; }
    public string? DesignerReviewNote { get; set; }
    public RoomPlannerMetadataDocument Metadata { get; set; } = new();
}
