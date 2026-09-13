using FurniSpace.Application.DTOs.RoomPlannerDocuments;

namespace FurniSpace.Application.DTOs.RoomPlanner;

public class RoomPlannerScenePayloadDto
{
    public RoomPlannerScenePayloadDto()
    {
        Camera = new RoomPlannerCameraDocument();
        Lighting = new RoomPlannerLightingDocument();
        Validation = new RoomPlannerValidationDocument();
    }

    public int SchemaVersion { get; set; } = 3;
    public string? EditorVersion { get; set; }
    public string Unit { get; set; } = "meter";
    public RoomPlannerBlueprintLayoutDocument? BlueprintLayout { get; set; }
    public List<RoomPlannerObjectDocument> Objects { get; set; } = [];
    public List<RoomPlannerLayerDocument> Layers { get; set; } = [];
    public string? StylePreset { get; set; }
    public RoomPlannerCameraDocument Camera { get; set; }
    public RoomPlannerLightingDocument Lighting { get; set; }
    public RoomPlannerValidationDocument Validation { get; set; }
    public RoomPlannerEditorStateDocument? EditorState { get; set; }
}
