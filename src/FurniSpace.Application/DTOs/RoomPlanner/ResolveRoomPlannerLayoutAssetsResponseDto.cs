namespace FurniSpace.Application.DTOs.RoomPlanner;

public sealed class ResolveRoomPlannerLayoutAssetsResponseDto
{
    public Guid SceneId { get; set; }
    public Guid ProjectId { get; set; }
    public IReadOnlyList<RoomPlannerResolvedLayoutAssetDto> Items { get; set; } = [];
}
