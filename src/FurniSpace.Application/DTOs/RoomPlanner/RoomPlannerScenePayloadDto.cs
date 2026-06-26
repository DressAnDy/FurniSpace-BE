using FurniSpace.Application.DTOs.RoomPlannerDocuments;
using FurniSpace.Shared.DTOs.RoomPlanner;

namespace FurniSpace.Application.DTOs.RoomPlanner;

public class RoomPlannerScenePayloadDto
    : RoomPlannerScenePayloadBase<
        RoomPlannerLayoutDocument,
        RoomPlannerObjectDocument,
        RoomPlannerLayerDocument,
        RoomPlannerCameraDocument,
        RoomPlannerLightingDocument,
        RoomPlannerValidationDocument,
        RoomPlannerEditorStateDocument>
{
    public RoomPlannerScenePayloadDto()
    {
        Layout = new RoomPlannerLayoutDocument();
        Camera = new RoomPlannerCameraDocument();
        Lighting = new RoomPlannerLightingDocument();
        Validation = new RoomPlannerValidationDocument();
    }
}
