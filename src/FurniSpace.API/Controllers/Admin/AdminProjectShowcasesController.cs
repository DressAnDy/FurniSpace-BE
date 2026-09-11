#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.ProjectShowcases;
using FurniSpace.Application.Interfaces.ProjectShowcases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Admin;

[Authorize(Roles = "ADMIN")]
[Route("admin/project-showcases")]
public sealed class AdminProjectShowcasesController : BaseApiController
{
    private readonly IProjectShowcaseService _showcases;

    public AdminProjectShowcasesController(IProjectShowcaseService showcases)
    {
        _showcases = showcases;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] AdminProjectShowcaseQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _showcases.GetAdminListAsync(query, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{showcaseId:guid}")]
    public async Task<IActionResult> GetDetail(
        Guid showcaseId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _showcases.GetAdminDetailAsync(showcaseId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
