#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.ProjectSchedules;
using FurniSpace.Application.Interfaces.ProjectSchedules;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers;

[Authorize]
[Route("projects/{projectId:guid}/schedules")]
public sealed class ProjectSchedulesController : BaseApiController
{
    private readonly IProjectScheduleService _schedules;

    public ProjectSchedulesController(IProjectScheduleService schedules)
    {
        _schedules = schedules;
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpPost]
    public async Task<IActionResult> Create(
        Guid projectId,
        [FromBody] CreateProjectScheduleRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _schedules.CreateAsync(projectId, currentUserId, request, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,ADMIN")]
    [HttpGet]
    public async Task<IActionResult> GetList(
        Guid projectId,
        [FromQuery] ProjectScheduleType? scheduleType = null,
        [FromQuery] ProjectScheduleStatus? status = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _schedules.GetListByProjectAsync(
            projectId,
            currentUserId,
            new ProjectScheduleListQueryDto
            {
                ScheduleType = scheduleType,
                Status = status,
                From = from,
                To = to,
                Page = page,
                Limit = limit
            },
            cancellationToken);

        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
