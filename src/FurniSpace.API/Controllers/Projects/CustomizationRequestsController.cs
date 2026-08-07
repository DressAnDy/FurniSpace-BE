#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.CustomizationRequests;
using FurniSpace.Application.Interfaces.CustomizationRequests;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Projects;

[Authorize]
[Route("")]
public sealed class CustomizationRequestsController : BaseApiController
{
    private readonly ICustomizationRequestService _customizationRequests;

    public CustomizationRequestsController(ICustomizationRequestService customizationRequests)
    {
        _customizationRequests = customizationRequests;
    }

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,PRODUCTION,ADMIN")]
    [HttpGet("projects/{projectId:guid}/customization-requests")]
    public async Task<IActionResult> GetByProject(
        Guid projectId,
        [FromQuery] Guid? proposalId = null,
        [FromQuery] Guid? sourceProductVersionId = null,
        [FromQuery] CustomizationStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _customizationRequests.GetByProjectAsync(
            projectId,
            currentUserId,
            new CustomizationRequestQueryDto
            {
                ProposalId = proposalId,
                SourceProductVersionId = sourceProductVersionId,
                Status = status
            },
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,PRODUCTION,ADMIN")]
    [HttpGet("customization-requests/{customizationRequestId:guid}")]
    public async Task<IActionResult> GetDetail(
        Guid customizationRequestId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _customizationRequests.GetDetailAsync(
            customizationRequestId,
            currentUserId,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,PRODUCTION,ADMIN")]
    [HttpGet("customization-requests/{customizationRequestId:guid}/versions")]
    public async Task<IActionResult> GetVersions(
        Guid customizationRequestId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _customizationRequests.GetVersionsAsync(
            customizationRequestId,
            currentUserId,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,PRODUCTION,ADMIN")]
    [HttpGet("customization-requests/{customizationRequestId:guid}/versions/{customizationRequestVersionId:guid}")]
    public async Task<IActionResult> GetVersionDetail(
        Guid customizationRequestId,
        Guid customizationRequestVersionId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _customizationRequests.GetVersionDetailAsync(
            customizationRequestId,
            customizationRequestVersionId,
            currentUserId,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,DESIGNER,ADMIN")]
    [HttpPost("proposal-items/{proposalItemId:guid}/customization-requests")]
    public async Task<IActionResult> Submit(
        Guid proposalItemId,
        [FromBody] SubmitCustomizationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _customizationRequests.SubmitAsync(
            proposalItemId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "DESIGNER,ADMIN")]
    [HttpPost("customization-requests/{customizationRequestId:guid}/versions")]
    public async Task<IActionResult> CreateVersion(
        Guid customizationRequestId,
        [FromBody] CreateCustomizationRequestVersionDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _customizationRequests.CreateVersionAsync(
            customizationRequestId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "DESIGNER,ADMIN")]
    [HttpPatch("customization-requests/{customizationRequestId:guid}/versions/{customizationRequestVersionId:guid}")]
    public async Task<IActionResult> UpdateDraftVersion(
        Guid customizationRequestId,
        Guid customizationRequestVersionId,
        [FromBody] UpdateCustomizationRequestVersionDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _customizationRequests.UpdateDraftVersionAsync(
            customizationRequestId,
            customizationRequestVersionId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "DESIGNER,ADMIN")]
    [HttpPost("customization-requests/{customizationRequestId:guid}/versions/{customizationRequestVersionId:guid}/submit-for-review")]
    public async Task<IActionResult> SubmitVersionForReview(
        Guid customizationRequestId,
        Guid customizationRequestVersionId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _customizationRequests.SubmitVersionForReviewAsync(
            customizationRequestId,
            customizationRequestVersionId,
            currentUserId,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "DESIGNER,ADMIN")]
    [HttpPost("customization-requests/{customizationRequestId:guid}/versions/{customizationRequestVersionId:guid}/withdraw")]
    public async Task<IActionResult> WithdrawVersion(
        Guid customizationRequestId,
        Guid customizationRequestVersionId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _customizationRequests.WithdrawVersionAsync(
            customizationRequestId,
            customizationRequestVersionId,
            currentUserId,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER")]
    [HttpPost("customization-requests/{customizationRequestId:guid}/accept")]
    public async Task<IActionResult> AcceptVersion(
        Guid customizationRequestId,
        [FromBody] AcceptCustomizationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _customizationRequests.AcceptVersionAsync(
            customizationRequestId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,ADMIN")]
    [HttpPatch("customization-requests/{customizationRequestId:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        Guid customizationRequestId,
        [FromBody] CancelCustomizationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _customizationRequests.CancelAsync(
            customizationRequestId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
