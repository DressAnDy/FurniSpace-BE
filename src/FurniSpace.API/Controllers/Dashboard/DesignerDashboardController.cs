#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Dashboard;
using FurniSpace.Application.Interfaces.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Dashboard;

[Route("api/dashboard/designer")]
public sealed class DesignerDashboardController : BaseApiController
{
    private readonly IDashboardQueueService _dashboard;

    public DesignerDashboardController(IDashboardQueueService dashboard)
    {
        _dashboard = dashboard;
    }

    [Authorize(Roles = "DESIGNER,ADMIN")]
    [HttpGet("work-queue")]
    public async Task<IActionResult> GetWorkQueue(
        [FromQuery] DashboardQueueQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _dashboard.GetDesignerWorkQueueAsync(currentUserId, query, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "DESIGNER,ADMIN")]
    [HttpGet("kpis")]
    public async Task<IActionResult> GetKpis(
        [FromQuery] DashboardQueueQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _dashboard.GetDesignerKpisAsync(currentUserId, query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// List for Confirmed Measurements KPI. Same scope + dateRange as
    /// <c>measurementDue</c> / <c>confirmedMeasurements</c>. <c>total</c> matches the KPI.
    /// dateRange uses Asia/Ho_Chi_Minh; thisWeek is Monday–Sunday.
    /// </summary>
    [Authorize(Roles = "DESIGNER,ADMIN")]
    [HttpGet("kpis/confirmed-measurements")]
    public async Task<IActionResult> GetConfirmedMeasurements(
        [FromQuery] DashboardQueueQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _dashboard.GetDesignerConfirmedMeasurementsAsync(
            currentUserId,
            query,
            cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// List for Proposal Consulting KPI. Same scope + dateRange as
    /// <c>proposalsInProgress</c> / <c>proposalConsultingProjects</c>.
    /// dateRange filters <c>updatedAt</c> (fallback <c>createdAt</c>) in Asia/Ho_Chi_Minh.
    /// </summary>
    [Authorize(Roles = "DESIGNER,ADMIN")]
    [HttpGet("kpis/proposal-consulting")]
    public async Task<IActionResult> GetProposalConsulting(
        [FromQuery] DashboardQueueQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _dashboard.GetDesignerProposalConsultingAsync(
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
