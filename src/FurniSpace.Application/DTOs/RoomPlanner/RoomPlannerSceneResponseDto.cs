namespace FurniSpace.Application.DTOs.RoomPlanner;

using FurniSpace.Shared.DTOs.Proposals;

public sealed class RoomPlannerSceneResponseDto : RoomPlannerScenePayloadDto
{
    public Guid SceneId { get; set; }
    public string? MongoSceneId { get; set; }
    public Guid? ProposalId { get; set; }
    public Guid? ProjectId { get; set; }
    public IReadOnlyList<Guid> ProjectAreaIds { get; set; } = [];
    public List<ProposalSceneAreaDto> Areas { get; set; } = [];
    public DateTime? LastSavedAt { get; set; }
}
