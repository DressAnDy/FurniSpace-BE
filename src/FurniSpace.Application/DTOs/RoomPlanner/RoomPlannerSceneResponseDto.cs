namespace FurniSpace.Application.DTOs.RoomPlanner;

public sealed class RoomPlannerSceneResponseDto : RoomPlannerScenePayloadDto
{
    public Guid SceneId { get; set; }
    public string? MongoSceneId { get; set; }
    public Guid? ProposalId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ProjectAreaId { get; set; }
    public IReadOnlyList<Guid> ProjectAreaIds { get; set; } = [];
    public DateTime? LastSavedAt { get; set; }
}
