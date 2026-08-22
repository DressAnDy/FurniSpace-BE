#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.ProjectShowcases;
using FurniSpace.Application.Interfaces.ProjectShowcases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Projects;

[Authorize]
[Route("projects/{projectId:guid}/showcase")]
public sealed class ProjectShowcasesController : BaseApiController
{
    private readonly IProjectShowcaseService _showcases;

    public ProjectShowcasesController(IProjectShowcaseService showcases)
    {
        _showcases = showcases;
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpPost]
    public async Task<IActionResult> Create(
        Guid projectId,
        [FromBody] CreateProjectShowcaseRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _showcases.CreateAsync(projectId, currentUserId, request, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,DESIGNER,ADMIN")]
    [HttpGet]
    public async Task<IActionResult> Get(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _showcases.GetByProjectAsync(projectId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
