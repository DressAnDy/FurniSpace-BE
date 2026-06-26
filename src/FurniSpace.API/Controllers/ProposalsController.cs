#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Proposals;
using FurniSpace.Application.Interfaces.Proposals;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers;

[Authorize]
[Route("")]
public sealed class ProposalsController : BaseApiController
{
    private readonly IProposalService _proposals;

    public ProposalsController(IProposalService proposals)
    {
        _proposals = proposals;
    }

    [Authorize(Roles = "DESIGNER,SALES,ADMIN")]
    [HttpPost("projects/{projectId:guid}/proposals")]
    public async Task<IActionResult> Create(
        Guid projectId,
        [FromBody] CreateProposalRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _proposals.CreateAsync(projectId, currentUserId, request, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,DESIGNER,SALES,ADMIN")]
    [HttpGet("projects/{projectId:guid}/proposals")]
    public async Task<IActionResult> GetListByProject(
        Guid projectId,
        [FromQuery] ProposalStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _proposals.GetListByProjectAsync(
            projectId,
            currentUserId,
            new ProposalListQueryDto
            {
                Status = status,
                Page = page,
                Limit = limit
            },
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "DESIGNER,SALES,ADMIN")]
    [HttpPost("proposals/{proposalId:guid}/scenes")]
    public async Task<IActionResult> CreateScene(
        Guid proposalId,
        [FromBody] CreateProposalSceneRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _proposals.CreateSceneAsync(proposalId, currentUserId, request, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,DESIGNER,SALES,ADMIN")]
    [HttpGet("proposals/{proposalId:guid}")]
    public async Task<IActionResult> GetDetail(
        Guid proposalId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _proposals.GetDetailAsync(proposalId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
