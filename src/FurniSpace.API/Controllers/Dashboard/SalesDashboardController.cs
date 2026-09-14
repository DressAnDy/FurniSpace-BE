#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Dashboard;
using FurniSpace.Application.Interfaces.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Dashboard;

[Route("api/dashboard/sales")]
public sealed class SalesDashboardController : BaseApiController
{
    private readonly IDashboardQueueService _dashboard;

    public SalesDashboardController(IDashboardQueueService dashboard)
    {
        _dashboard = dashboard;
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpGet("action-queue")]
    public async Task<IActionResult> GetActionQueue(
        [FromQuery] DashboardQueueQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _dashboard.GetSalesActionQueueAsync(currentUserId, query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Sales KPI stock counts. <c>acceptedProjects</c>, <c>unpaidRemaining</c>, and <c>overdueTasks</c>
    /// ignore <c>dateRange</c> and <c>search</c>.
    /// <c>overdueTasks</c> counts scoped projects whose <c>targetCompletionDate</c> is before today (UTC);
    /// no project status is excluded.
    /// </summary>
    [Authorize(Roles = "SALES,ADMIN")]
    [HttpGet("kpis")]
    public async Task<IActionResult> GetKpis(
        [FromQuery] DashboardQueueQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _dashboard.GetSalesKpisAsync(currentUserId, query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Stock list for the Unpaid Remaining KPI card. Same scope semantics as KPIs;
    /// ignores <c>dateRange</c> and <c>search</c>. <c>total</c> matches <c>unpaidRemaining</c>.
    /// </summary>
    [Authorize(Roles = "SALES,ADMIN")]
    [HttpGet("kpis/unpaid-remaining")]
    public async Task<IActionResult> GetUnpaidRemaining(
        [FromQuery] DashboardQueueQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _dashboard.GetSalesUnpaidRemainingAsync(currentUserId, query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Stock list for the Overdue Tasks KPI card. Same scope and deadline rule as KPIs
    /// (<c>targetCompletionDate</c> before today UTC; no status excluded).
    /// Ignores <c>dateRange</c> and <c>search</c>. <c>total</c> matches <c>overdueTasks</c>.
    /// </summary>
    [Authorize(Roles = "SALES,ADMIN")]
    [HttpGet("kpis/overdue-tasks")]
    public async Task<IActionResult> GetOverdueTasks(
        [FromQuery] DashboardQueueQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _dashboard.GetSalesOverdueTasksAsync(currentUserId, query, cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
