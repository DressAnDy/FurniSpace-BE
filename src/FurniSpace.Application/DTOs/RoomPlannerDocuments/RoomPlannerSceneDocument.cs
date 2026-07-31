namespace FurniSpace.Application.DTOs.RoomPlannerDocuments;

public sealed class RoomPlannerSceneDocument
{
    public string? Id { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public string? EditorVersion { get; set; }
    public Guid SqlSceneId { get; set; }
    public Guid ProposalId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProjectAreaId { get; set; }
    public string SceneKind { get; set; } = "OFFICIAL";
    public string Unit { get; set; } = "meter";
    public RoomPlannerSceneLinksDocument SceneLinks { get; set; } = new();
    public RoomPlannerBlueprintLayoutDocument? BlueprintLayout { get; set; }
    public RoomPlannerLayoutDocument? Layout { get; set; }
    public List<RoomPlannerObjectDocument> Objects { get; set; } = [];
    public List<RoomPlannerLayerDocument> Layers { get; set; } = [];
    public string? StylePreset { get; set; }
    public RoomPlannerCameraDocument Camera { get; set; } = new();
    public RoomPlannerLightingDocument Lighting { get; set; } = new();
    public RoomPlannerValidationDocument Validation { get; set; } = new();
    public RoomPlannerEditorStateDocument? EditorState { get; set; }
    public RoomPlannerMetadataDocument Metadata { get; set; } = new();
}
