#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.ProjectShowcases;
using FurniSpace.Application.Interfaces.ProjectShowcases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Projects;

[Authorize]
[Route("project-showcases/{showcaseId:guid}")]
public sealed class ProjectShowcaseWorkflowController : BaseApiController
{
    private readonly IProjectShowcaseService _showcases;

    public ProjectShowcaseWorkflowController(IProjectShowcaseService showcases)
    {
        _showcases = showcases;
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpPatch]
    public async Task<IActionResult> Update(
        Guid showcaseId,
        [FromBody] UpdateProjectShowcaseRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _showcases.UpdateAsync(showcaseId, currentUserId, request, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpPatch("submit")]
    public async Task<IActionResult> Submit(
        Guid showcaseId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _showcases.SubmitAsync(showcaseId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPatch("publish")]
    public async Task<IActionResult> Publish(
        Guid showcaseId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _showcases.PublishAsync(showcaseId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPatch("archive")]
    public async Task<IActionResult> Archive(
        Guid showcaseId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _showcases.ArchiveAsync(showcaseId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}

[Authorize]
[Route("project-showcases/{showcaseId:guid}/media")]
public sealed class ProjectShowcaseMediaController : BaseApiController
{
    private readonly IProjectShowcaseService _showcases;

    public ProjectShowcaseMediaController(IProjectShowcaseService showcases)
    {
        _showcases = showcases;
    }

    [Authorize(Roles = "SALES,DESIGNER,ADMIN")]
    [HttpPost]
    public async Task<IActionResult> Add(
        Guid showcaseId,
        [FromBody] AddProjectShowcaseMediaRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _showcases.AddMediaAsync(showcaseId, currentUserId, request, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,DESIGNER,ADMIN")]
    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder(
        Guid showcaseId,
        [FromBody] ReorderProjectShowcaseMediaRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _showcases.ReorderMediaAsync(showcaseId, currentUserId, request, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,DESIGNER,ADMIN")]
    [HttpPatch("{mediaId:guid}/cover")]
    public async Task<IActionResult> SetCover(
        Guid showcaseId,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _showcases.SetCoverAsync(showcaseId, mediaId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,DESIGNER,ADMIN")]
    [HttpDelete("{mediaId:guid}")]
    public async Task<IActionResult> Remove(
        Guid showcaseId,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _showcases.RemoveMediaAsync(showcaseId, mediaId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
