#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.ProjectSchedules;
using FurniSpace.Application.Interfaces.ProjectSchedules;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Projects;

[Authorize]
[Route("project-schedules")]
public sealed class ProjectSchedulesController : BaseApiController
{
    private readonly IProjectScheduleService _schedules;

    public ProjectSchedulesController(IProjectScheduleService schedules)
    {
        _schedules = schedules;
    }

    [Authorize(Roles = "SALES,PRODUCTION,ADMIN")]
    [HttpPost("{projectId:guid}")]
    [HttpPost("/projects/{projectId:guid}/schedules")]
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

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,PRODUCTION,ADMIN")]
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] Guid projectId,
        [FromQuery] ProjectScheduleListQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _schedules.GetListByProjectAsync(
            projectId,
            currentUserId,
            query,
            cancellationToken);

        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,DESIGNER,PRODUCTION,ADMIN")]
    [HttpGet("my-assigned")]
    public async Task<IActionResult> GetMyAssigned(
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

        var result = await _schedules.GetMyAssignedAsync(
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

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,PRODUCTION,ADMIN")]
    [HttpGet("{scheduleId:guid}")]
    public async Task<IActionResult> GetDetail(
        Guid scheduleId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _schedules.GetDetailAsync(scheduleId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,PRODUCTION,ADMIN")]
    [HttpPatch("{scheduleId:guid}")]
    public async Task<IActionResult> Update(
        Guid scheduleId,
        [FromBody] UpdateProjectScheduleRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _schedules.UpdateAsync(scheduleId, currentUserId, request, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,PRODUCTION,ADMIN")]
    [HttpPatch("{scheduleId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid scheduleId,
        [FromBody] UpdateProjectScheduleStatusRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _schedules.UpdateStatusAsync(scheduleId, currentUserId, request, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,PRODUCTION,ADMIN")]
    [HttpDelete("{scheduleId:guid}")]
    public async Task<IActionResult> Delete(
        Guid scheduleId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _schedules.DeleteAsync(scheduleId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
