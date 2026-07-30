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
        [FromQuery] Guid? productVersionId = null,
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
                ProductVersionId = productVersionId,
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
    [HttpPatch("customization-requests/{customizationRequestId:guid}/designer-review")]
    public async Task<IActionResult> DesignerReview(
        Guid customizationRequestId,
        [FromBody] DesignerReviewCustomizationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _customizationRequests.DesignerReviewAsync(
            customizationRequestId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "PRODUCTION,ADMIN")]
    [HttpPatch("customization-requests/{customizationRequestId:guid}/production-review")]
    public async Task<IActionResult> ProductionReview(
        Guid customizationRequestId,
        [FromBody] ProductionReviewCustomizationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _customizationRequests.ProductionReviewAsync(
            customizationRequestId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER")]
    [HttpPatch("customization-requests/{customizationRequestId:guid}/customer-decision")]
    public async Task<IActionResult> CustomerDecision(
        Guid customizationRequestId,
        [FromBody] CustomerDecisionCustomizationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _customizationRequests.CustomerDecisionAsync(
            customizationRequestId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "DESIGNER,ADMIN")]
    [HttpPost("customization-requests/{customizationRequestId:guid}/product-version")]
    public async Task<IActionResult> CreateProductVersion(
        Guid customizationRequestId,
        [FromBody] CreateCustomizationProductVersionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _customizationRequests.CreateCustomizationProductVersionAsync(
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
