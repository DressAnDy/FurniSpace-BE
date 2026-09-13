#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.ProjectAreas;
using FurniSpace.Application.Interfaces.ProjectAreas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Projects;

[Authorize]
[Route("project-areas")]
public sealed class ProjectAreasController : BaseApiController
{
    private readonly IProjectAreaService _areas;

    public ProjectAreasController(IProjectAreaService areas)
    {
        _areas = areas;
    }

    [Authorize(Roles = "SALES,DESIGNER,ADMIN")]
    [HttpPost("{projectId:guid}")]
    [HttpPost("/projects/{projectId:guid}/areas")]
    public async Task<IActionResult> Create(
        Guid projectId,
        [FromBody] CreateProjectAreaRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _areas.CreateAsync(projectId, currentUserId, request, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,ADMIN")]
    [HttpGet("/projects/{projectId:guid}/areas")]
    public async Task<IActionResult> GetList(
        Guid projectId,
        [FromQuery] bool includeCancelled = false,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _areas.GetListByProjectAsync(
            projectId,
            currentUserId,
            includeCancelled,
            cancellationToken);

        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,ADMIN")]
    [HttpGet("{projectAreaId:guid}")]
    public async Task<IActionResult> GetDetail(
        Guid projectAreaId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _areas.GetDetailAsync(projectAreaId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,DESIGNER,ADMIN")]
    [HttpPatch("{projectAreaId:guid}")]
    public async Task<IActionResult> Update(
        Guid projectAreaId,
        [FromBody] UpdateProjectAreaRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _areas.UpdateAsync(projectAreaId, currentUserId, request, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,DESIGNER,ADMIN")]
    [HttpPatch("{projectAreaId:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        Guid projectAreaId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _areas.CancelAsync(projectAreaId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
