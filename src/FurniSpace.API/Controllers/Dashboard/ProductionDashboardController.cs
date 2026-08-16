#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Dashboard;
using FurniSpace.Application.Interfaces.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Dashboard;

[Route("api/dashboard/production")]
public sealed class ProductionDashboardController : BaseApiController
{
    private readonly IDashboardQueueService _dashboard;

    public ProductionDashboardController(IDashboardQueueService dashboard)
    {
        _dashboard = dashboard;
    }

    [Authorize(Roles = "PRODUCTION,ADMIN")]
    [HttpGet("queue")]
    public async Task<IActionResult> GetQueue(
        [FromQuery] DashboardQueueQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _dashboard.GetProductionQueueAsync(currentUserId, query, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "PRODUCTION,ADMIN")]
    [HttpGet("kpis")]
    public async Task<IActionResult> GetKpis(
        [FromQuery] DashboardQueueQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _dashboard.GetProductionKpisAsync(currentUserId, query, cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
