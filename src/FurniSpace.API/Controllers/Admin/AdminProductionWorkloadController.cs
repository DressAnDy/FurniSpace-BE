#nullable enable

using FurniSpace.API.Base;
using FurniSpace.Application.Interfaces.Reports;
using FurniSpace.Shared.DTOs.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Admin;

[Authorize(Roles = "ADMIN")]
[Route("admin/production")]
public sealed class AdminProductionWorkloadController : BaseApiController
{
    private readonly IAdminReportService _reports;

    public AdminProductionWorkloadController(IAdminReportService reports)
    {
        _reports = reports;
    }

    [HttpGet("workload")]
    public async Task<IActionResult> GetWorkload(
        [FromQuery] ProductionWorkloadQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _reports.GetProductionWorkloadAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("workload/summary")]
    public async Task<IActionResult> GetWorkloadSummary(CancellationToken cancellationToken = default)
    {
        var result = await _reports.GetProductionWorkloadSummaryAsync(cancellationToken);
        return ToActionResult(result);
    }
}
