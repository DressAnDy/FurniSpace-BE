#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.ProductIssues;
using FurniSpace.Application.Interfaces.ProductIssues;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Projects;

[Authorize]
[Route("")]
public sealed class ProductIssuesController : BaseApiController
{
    private readonly IDeliveryProductIssueReportService _productIssues;

    public ProductIssuesController(IDeliveryProductIssueReportService productIssues)
    {
        _productIssues = productIssues;
    }

    [Authorize(Roles = "CUSTOMER")]
    [HttpPost("orders/{orderId:guid}/product-issues")]
    [Consumes("application/json")]
    public async Task<IActionResult> Create(
        Guid orderId,
        [FromBody] CreateProductIssueRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _productIssues.CreateAsync(
            orderId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER")]
    [HttpPost("orders/{orderId:guid}/product-issues/evidence/upload-url")]
    public async Task<IActionResult> PrepareEvidenceUpload(
        Guid orderId,
        [FromBody] PrepareProductIssueEvidenceUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _productIssues.PrepareEvidenceUploadAsync(
            orderId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER")]
    [HttpPost("orders/{orderId:guid}/product-issues/evidence/complete")]
    public async Task<IActionResult> CompleteEvidenceUpload(
        Guid orderId,
        [FromBody] CompleteProductIssueEvidenceUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _productIssues.CompleteEvidenceUploadAsync(
            orderId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,PRODUCTION,ADMIN")]
    [HttpGet("orders/{orderId:guid}/product-issues")]
    public async Task<IActionResult> GetByOrder(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _productIssues.GetByOrderAsync(orderId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,PRODUCTION,ADMIN")]
    [HttpGet("projects/{projectId:guid}/product-issues")]
    public async Task<IActionResult> GetByProject(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _productIssues.GetByProjectAsync(projectId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,PRODUCTION,ADMIN")]
    [HttpGet("product-issues/{issueId:guid}")]
    public async Task<IActionResult> GetDetail(
        Guid issueId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _productIssues.GetDetailAsync(issueId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,PRODUCTION,ADMIN")]
    [HttpPatch("product-issues/{issueId:guid}/resolve")]
    public async Task<IActionResult> Resolve(
        Guid issueId,
        [FromBody] ResolveProductIssueRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _productIssues.ResolveAsync(issueId, currentUserId, request, cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
