#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Proposals;
using FurniSpace.Application.Interfaces.Proposals;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Projects;

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
    [HttpGet("proposals/{proposalId:guid}/scenes")]
    public async Task<IActionResult> GetScenes(
        Guid proposalId,
        [FromQuery] ProposalSceneType? sceneType = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _proposals.GetScenesAsync(
            proposalId,
            currentUserId,
            new ProposalSceneListQueryDto
            {
                SceneType = sceneType,
                IsActive = isActive,
                Page = page,
                Limit = limit
            },
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,DESIGNER,SALES,ADMIN")]
    [HttpGet("proposal-scenes/{sceneId:guid}")]
    public async Task<IActionResult> GetSceneDetail(
        Guid sceneId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _proposals.GetSceneDetailAsync(sceneId, currentUserId, cancellationToken);
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

    [Authorize(Roles = "DESIGNER,ADMIN")]
    [HttpPost("proposals/{proposalId:guid}/items/sync-from-scene")]
    public async Task<IActionResult> SyncItemsFromScene(
        Guid proposalId,
        [FromBody] SyncProposalItemsFromSceneRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _proposals.SyncItemsFromSceneAsync(
            proposalId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,DESIGNER,SALES,ADMIN")]
    [HttpGet("proposals/{proposalId:guid}/items")]
    public async Task<IActionResult> GetItems(
        Guid proposalId,
        [FromQuery] Guid? sceneId = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _proposals.GetItemsAsync(
            proposalId,
            currentUserId,
            new ProposalItemListQueryDto
            {
                SceneId = sceneId,
                Page = page,
                Limit = limit
            },
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "DESIGNER,SALES,ADMIN")]
    [HttpPatch("proposal-items/{proposalItemId:guid}")]
    public async Task<IActionResult> UpdateItem(
        Guid proposalItemId,
        [FromBody] UpdateProposalItemRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _proposals.UpdateItemAsync(
            proposalItemId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "DESIGNER,SALES,ADMIN")]
    [HttpDelete("proposal-items/{proposalItemId:guid}")]
    public async Task<IActionResult> DeleteItem(
        Guid proposalItemId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _proposals.DeleteItemAsync(proposalItemId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER")]
    [HttpPatch("proposals/{proposalId:guid}/select-final")]
    public async Task<IActionResult> SelectFinal(
        Guid proposalId,
        [FromBody] SelectFinalProposalRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _proposals.SelectFinalAsync(
            proposalId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER")]
    [HttpPatch("proposals/{proposalId:guid}/request-revision")]
    public async Task<IActionResult> RequestRevision(
        Guid proposalId,
        [FromBody] RequestProposalRevisionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _proposals.RequestRevisionAsync(
            proposalId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "DESIGNER,SALES,ADMIN")]
    [HttpPatch("proposals/{proposalId:guid}/publish")]
    public async Task<IActionResult> Publish(
        Guid proposalId,
        [FromBody] PublishProposalRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _proposals.PublishAsync(
            proposalId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "DESIGNER,SALES,ADMIN")]
    [HttpPost("proposals/{proposalId:guid}/reopen-for-editing")]
    public async Task<IActionResult> ReopenForEditing(
        Guid proposalId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _proposals.ReopenForEditingAsync(proposalId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "DESIGNER,SALES,ADMIN")]
    [HttpPatch("proposals/{proposalId:guid}")]
    public async Task<IActionResult> Update(
        Guid proposalId,
        [FromBody] UpdateProposalRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _proposals.UpdateAsync(
            proposalId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "DESIGNER,SALES,ADMIN")]
    [HttpPatch("proposal-scenes/{sceneId:guid}")]
    public async Task<IActionResult> UpdateScene(
        Guid sceneId,
        [FromBody] UpdateProposalSceneRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _proposals.UpdateSceneAsync(
            sceneId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
