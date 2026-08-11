#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.RoomPlanner;
using FurniSpace.Application.Interfaces.RoomPlanner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Projects;

[Authorize]
[Route("proposal-scenes")]
public sealed class RoomPlannerScenesController : BaseApiController
{
    private readonly IRoomPlannerSceneService _roomPlannerScenes;

    public RoomPlannerScenesController(IRoomPlannerSceneService roomPlannerScenes)
    {
        _roomPlannerScenes = roomPlannerScenes;
    }

    [Authorize(Roles = "CUSTOMER,DESIGNER,SALES,ADMIN")]
    [HttpGet("{sceneId:guid}/room-planner")]
    public async Task<IActionResult> GetScene(
        Guid sceneId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var currentUserId, out var roleName))
        {
            return Unauthorized();
        }

        var result = await _roomPlannerScenes.GetSceneAsync(
            sceneId,
            currentUserId,
            roleName,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,DESIGNER,SALES,ADMIN")]
    [HttpPost("{sceneId:guid}/room-planner/resolve-products")]
    public async Task<IActionResult> ResolveProducts(
        Guid sceneId,
        [FromBody] ResolveRoomPlannerProductsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var currentUserId, out var roleName))
        {
            return Unauthorized();
        }

        var result = await _roomPlannerScenes.ResolveProductsAsync(
            sceneId,
            request,
            currentUserId,
            roleName,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "DESIGNER,ADMIN")]
    [HttpPut("{sceneId:guid}/room-planner")]
    public async Task<IActionResult> SaveScene(
        Guid sceneId,
        [FromBody] RoomPlannerScenePayloadDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var currentUserId, out var roleName))
        {
            return Unauthorized();
        }

        var result = await _roomPlannerScenes.SaveSceneAsync(
            sceneId,
            request,
            currentUserId,
            roleName,
            cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUser(out Guid currentUserId, out string roleName)
    {
        roleName = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
