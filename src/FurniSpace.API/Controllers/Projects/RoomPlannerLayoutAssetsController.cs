#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.LayoutAssets;
using FurniSpace.Application.Interfaces.LayoutAssets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Projects;

[Authorize]
[Route("room-planner/layout-assets")]
public sealed class RoomPlannerLayoutAssetsController : BaseApiController
{
    private readonly ILayoutAssetService _layoutAssets;

    public RoomPlannerLayoutAssetsController(ILayoutAssetService layoutAssets)
    {
        _layoutAssets = layoutAssets;
    }

    [Authorize(Roles = "DESIGNER,ADMIN")]
    [HttpGet]
    public async Task<IActionResult> GetCatalog(
        [FromQuery] RoomPlannerLayoutAssetCatalogQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var roleName = User.FindFirstValue(ClaimTypes.Role);
        var result = await _layoutAssets.GetRoomPlannerCatalogAsync(query, roleName, cancellationToken);
        return ToActionResult(result);
    }
}
