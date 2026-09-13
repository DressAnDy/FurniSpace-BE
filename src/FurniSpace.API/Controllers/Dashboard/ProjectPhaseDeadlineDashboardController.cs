#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Dashboard;
using FurniSpace.Application.Interfaces.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Dashboard;

[Route("dashboard")]
public sealed class ProjectPhaseDeadlineDashboardController : BaseApiController
{
    private readonly IDashboardQueueService _dashboard;

    public ProjectPhaseDeadlineDashboardController(IDashboardQueueService dashboard)
    {
        _dashboard = dashboard;
    }

    [Authorize(Roles = "SALES,DESIGNER,PRODUCTION,ADMIN")]
    [HttpGet("project-phase-deadlines")]
    [HttpGet("/api/dashboard/project-phase-deadlines")]
    public async Task<IActionResult> GetProjectPhaseDeadlines(
        [FromQuery] ProjectPhaseDeadlineRiskQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _dashboard.GetProjectPhaseDeadlineRisksAsync(
            currentUserId,
            query,
            cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
