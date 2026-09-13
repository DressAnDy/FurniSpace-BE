#nullable enable

namespace FurniSpace.Application.DTOs.RoomPlanner;

public sealed class ResolveRoomPlannerProductsRequestDto
{
    public IReadOnlyList<Guid> ProductVersionIds { get; set; } = [];
}
