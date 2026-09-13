namespace FurniSpace.Application.DTOs.RoomPlanner;

public sealed class ResolveRoomPlannerProductsResponseDto
{
    public Guid SceneId { get; set; }
    public Guid ProjectId { get; set; }
    public IReadOnlyList<RoomPlannerResolvedProductDto> Items { get; set; } = [];
}
