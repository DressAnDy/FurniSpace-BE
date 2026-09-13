#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.OperationalDelayReports;
using FurniSpace.Application.Interfaces.OperationalDelayReports;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Projects;

[Authorize]
[Route("")]
public sealed class OperationalDelayReportsController : BaseApiController
{
    private readonly IOperationalDelayReportService _delayReports;

    public OperationalDelayReportsController(IOperationalDelayReportService delayReports)
    {
        _delayReports = delayReports;
    }

    [Authorize(Roles = "SALES,PRODUCTION,ADMIN")]
    [HttpPost("projects/{projectId:guid}/delay-reports/production")]
    public async Task<IActionResult> CreateProductionReport(
        Guid projectId,
        [FromBody] CreateProductionDelayReportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _delayReports.CreateProductionReportAsync(
            projectId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,PRODUCTION,ADMIN")]
    [HttpPost("projects/{projectId:guid}/delay-reports/delivery")]
    public async Task<IActionResult> CreateDeliveryReport(
        Guid projectId,
        [FromBody] CreateDeliveryDelayReportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _delayReports.CreateDeliveryReportAsync(
            projectId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,PRODUCTION,ADMIN")]
    [HttpGet("projects/{projectId:guid}/delay-reports")]
    public async Task<IActionResult> GetByProject(
        Guid projectId,
        [FromQuery] OperationalDelayPhase phase,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _delayReports.GetByProjectAsync(
            projectId,
            currentUserId,
            phase,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,PRODUCTION,ADMIN")]
    [HttpGet("delay-reports/{reportId:guid}")]
    public async Task<IActionResult> GetDetail(
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _delayReports.GetDetailAsync(reportId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
