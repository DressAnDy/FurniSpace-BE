#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Application.Interfaces.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Projects;

[Authorize]
[Route("projects/{projectId:guid}/phase-deadlines")]
public sealed class ProjectPhaseDeadlinesController : BaseApiController
{
    private readonly IProjectPhaseDeadlineService _phaseDeadlines;

    public ProjectPhaseDeadlinesController(IProjectPhaseDeadlineService phaseDeadlines)
    {
        _phaseDeadlines = phaseDeadlines;
    }

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,PRODUCTION,ADMIN")]
    [HttpGet]
    public async Task<IActionResult> Get(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _phaseDeadlines.GetAsync(projectId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpPut]
    public async Task<IActionResult> Upsert(
        Guid projectId,
        [FromBody] UpsertProjectPhaseDeadlinesRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _phaseDeadlines.UpsertAsync(projectId, currentUserId, request, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpPut("production")]
    public async Task<IActionResult> UpsertProductionDeadline(
        Guid projectId,
        [FromBody] UpsertProductionPhaseDeadlineRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _phaseDeadlines.UpsertProductionDeadlineAsync(
            projectId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
