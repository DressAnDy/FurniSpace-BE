using FurniSpace.Application.DTOs.RoomPlannerDocuments;

namespace FurniSpace.Application.DTOs.RoomPlanner;

public sealed class SaveRoomPlannerSceneRequestDto
{
    public int SchemaVersion { get; set; } = 1;
    public string Unit { get; set; } = "meter";
    public RoomPlannerLayoutDocument Layout { get; set; } = new();
    public List<RoomPlannerObjectDocument> Objects { get; set; } = [];
    public List<RoomPlannerLayerDocument> Layers { get; set; } = [];
    public string? StylePreset { get; set; }
    public RoomPlannerCameraDocument Camera { get; set; } = new();
    public RoomPlannerLightingDocument Lighting { get; set; } = new();
    public RoomPlannerValidationDocument Validation { get; set; } = new();
    public RoomPlannerEditorStateDocument? EditorState { get; set; }
}
