#nullable enable

using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Reports;
using FurniSpace.Application.Interfaces.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Admin;

[Authorize(Roles = "ADMIN")]
[Route("admin/project-reports")]
public sealed class AdminProjectReportsController : BaseApiController
{
    private readonly IAdminProjectReportService _reports;

    public AdminProjectReportsController(IAdminProjectReportService reports)
    {
        _reports = reports;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] AdminProjectReportsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _reports.GetListAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{projectId:guid}")]
    public async Task<IActionResult> GetDetail(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var result = await _reports.GetDetailAsync(projectId, cancellationToken);
        return ToActionResult(result);
    }
}
