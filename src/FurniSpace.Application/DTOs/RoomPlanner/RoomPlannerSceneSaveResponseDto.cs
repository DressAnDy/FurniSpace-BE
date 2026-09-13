namespace FurniSpace.Application.DTOs.RoomPlanner;

public sealed class RoomPlannerSceneSaveResponseDto
{
    public Guid SceneId { get; set; }
    public string MongoSceneId { get; set; } = string.Empty;
    public DateTime LastSavedAt { get; set; }
}
