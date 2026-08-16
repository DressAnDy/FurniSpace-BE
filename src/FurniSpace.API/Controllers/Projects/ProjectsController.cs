#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.Interfaces.Proposals;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Application.Interfaces.ProjectChatMessages;
using FurniSpace.Application.Interfaces.Projects;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Projects;

[Authorize]
[Route("projects")]
public sealed class ProjectsController : BaseApiController
{
    private readonly IProjectService _projects;
    private readonly IProjectChatMessageService _chatMessages;
    private readonly IProposalService _proposals;

    public ProjectsController(
        IProjectService projects,
        IProjectChatMessageService chatMessages,
        IProposalService proposals)
    {
        _projects = projects;
        _chatMessages = chatMessages;
        _proposals = proposals;
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

    [Authorize(Roles = "SALES,ADMIN,CUSTOMER,DESIGNER")]
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

    [Authorize(Roles = "ADMIN,SALES,DESIGNER,CUSTOMER")]
    [HttpGet("by-user/{userId:guid}")]
    public async Task<IActionResult> GetByUser(
        Guid userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] ProjectStatus? status = null,
        [FromQuery] string? roleScope = null,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projects.GetByUserAsync(
            userId,
            currentUserId,
            new GetProjectsByUserQueryDto
            {
                Page = page,
                PageSize = pageSize,
                Status = status,
                RoleScope = roleScope,
                Keyword = keyword
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

    [Authorize(Roles = "CUSTOMER")]
    [HttpGet("{projectId:guid}/published-proposal")]
    public async Task<IActionResult> GetPublishedProposal(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _proposals.GetPublishedByProjectAsync(projectId, currentUserId, cancellationToken);
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

    [Authorize(Roles = "CUSTOMER,SALES,ADMIN")]
    [HttpPatch("{projectId:guid}/basic-information")]
    public async Task<IActionResult> UpdateBasicInformation(
        Guid projectId,
        [FromBody] UpdateProjectBasicInformationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projects.UpdateBasicInformationAsync(projectId, currentUserId, request, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,ADMIN")]
    [HttpPatch("{projectId:guid}/target-completion-date")]
    public async Task<IActionResult> UpdateTargetCompletionDate(
        Guid projectId,
        [FromBody] UpdateProjectTargetCompletionDateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projects.UpdateTargetCompletionDateAsync(projectId, currentUserId, request, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,DESIGNER,ADMIN")]
    [HttpPatch("{projectId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid projectId,
        [FromBody] UpdateProjectStatusRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projects.UpdateStatusAsync(projectId, currentUserId, request, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpPatch("{projectId:guid}/rejection")]
    public async Task<IActionResult> Reject(
        Guid projectId,
        [FromBody] RejectProjectRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projects.RejectAsync(projectId, currentUserId, request, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpPatch("{projectId:guid}/complete")]
    public async Task<IActionResult> Complete(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projects.CompleteAsync(projectId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,ADMIN")]
    [HttpPost("{projectId:guid}/reopen-proposal")]
    public async Task<IActionResult> ReopenProposal(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projects.ReopenProposalAsync(projectId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpPatch("{projectId:guid}/designer-assignment")]
    public async Task<IActionResult> AssignDesigner(
        Guid projectId,
        [FromBody] AssignProjectDesignerRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projects.AssignDesignerAsync(projectId, currentUserId, request, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,ADMIN,CUSTOMER,DESIGNER")]
    [HttpGet("{projectId:guid}/chat-messages/search")]
    public async Task<IActionResult> SearchChatMessages(
        Guid projectId,
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _chatMessages.SearchProjectMessagesAsync(
            projectId,
            currentUserId,
            q,
            page,
            limit,
            cancellationToken);

        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
