#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Application.Interfaces.Projects;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers;

[Authorize]
[Route("projects")]
public sealed class ProjectsController : BaseApiController
{
    private readonly IProjectService _projects;

    public ProjectsController(IProjectService projects)
    {
        _projects = projects;
    }

    [Authorize(Roles = "CUSTOMER")]
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateProjectRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projects.CreateAsync(currentUserId, request, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,ADMIN,CUSTOMER")]
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] ProjectStatus? status = null,
        [FromQuery] Guid? assignedSalesId = null,
        [FromQuery] Guid? assignedDesignerId = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projects.GetListAsync(
            currentUserId,
            new ProjectListQueryDto
            {
                Status = status,
                AssignedSalesId = assignedSalesId,
                AssignedDesignerId = assignedDesignerId,
                Search = search,
                Page = page,
                Limit = limit
            },
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,ADMIN,CUSTOMER,DESIGNER")]
    [HttpGet("{projectId:guid}")]
    public async Task<IActionResult> GetById(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projects.GetByIdAsync(projectId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpPatch("{projectId:guid}/sales-assignment")]
    public async Task<IActionResult> AssignSales(
        Guid projectId,
        [FromBody] AssignProjectSalesRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projects.AssignSalesAsync(projectId, currentUserId, request, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpPost("{projectId:guid}/information-requests")]
    public async Task<IActionResult> RequestInformation(
        Guid projectId,
        [FromBody] RequestProjectInformationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projects.RequestInformationAsync(projectId, currentUserId, request, cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
