namespace FurniSpace.Application.DTOs.RoomPlanner;

public sealed class RoomPlannerSceneResponseDto : RoomPlannerScenePayloadDto
{
    public Guid SceneId { get; set; }
    public string? MongoSceneId { get; set; }
    public DateTime? LastSavedAt { get; set; }
}
