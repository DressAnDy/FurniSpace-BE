#nullable enable

namespace FurniSpace.Application.DTOs.RoomPlanner;

public sealed class ResolveRoomPlannerLayoutAssetsRequestDto
{
    public IReadOnlyList<Guid> LayoutAssetIds { get; set; } = [];
}
