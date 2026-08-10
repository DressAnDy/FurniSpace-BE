#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Quotations;
using FurniSpace.Application.Interfaces.Quotations;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Projects;

[Authorize]
[Route("")]
public sealed class QuotationsController : BaseApiController
{
    private readonly IQuotationService _quotations;

    public QuotationsController(IQuotationService quotations)
    {
        _quotations = quotations;
    }

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,ADMIN")]
    [HttpGet("projects/{projectId:guid}/quotations")]
    public async Task<IActionResult> GetByProject(
        Guid projectId,
        [FromQuery] QuotationStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _quotations.GetByProjectAsync(
            projectId,
            currentUserId,
            new QuotationQueryDto { Status = status },
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,ADMIN")]
    [HttpGet("quotations/{quotationId:guid}")]
    public async Task<IActionResult> GetDetail(
        Guid quotationId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _quotations.GetDetailAsync(
            quotationId,
            currentUserId,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpPost("projects/{projectId:guid}/quotations")]
    public async Task<IActionResult> CreateDraft(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _quotations.CreateDraftAsync(
            projectId,
            currentUserId,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpPatch("quotations/{quotationId:guid}")]
    public async Task<IActionResult> Update(
        Guid quotationId,
        [FromBody] UpdateQuotationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _quotations.UpdateAsync(
            quotationId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpPatch("quotations/{quotationId:guid}/items/{quotationItemId:guid}/financials")]
    public async Task<IActionResult> UpdateItemFinancials(
        Guid quotationId,
        Guid quotationItemId,
        [FromBody] UpdateQuotationItemFinancialsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _quotations.UpdateItemFinancialsAsync(
            quotationId,
            quotationItemId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpPut("quotations/{quotationId:guid}/items/financials")]
    public async Task<IActionResult> BulkUpdateItemFinancials(
        Guid quotationId,
        [FromBody] BulkUpdateQuotationItemFinancialsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _quotations.BulkUpdateItemFinancialsAsync(
            quotationId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpPatch("quotations/{quotationId:guid}/send")]
    public async Task<IActionResult> Send(
        Guid quotationId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _quotations.SendAsync(
            quotationId,
            currentUserId,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER")]
    [HttpPatch("quotations/{quotationId:guid}/accept")]
    public async Task<IActionResult> Accept(
        Guid quotationId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _quotations.AcceptAsync(
            quotationId,
            currentUserId,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER")]
    [HttpPatch("quotations/{quotationId:guid}/request-revision")]
    public async Task<IActionResult> RequestRevision(
        Guid quotationId,
        [FromBody] RequestQuotationRevisionDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _quotations.RequestRevisionAsync(
            quotationId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpPatch("quotations/{quotationId:guid}/revise")]
    public async Task<IActionResult> Revise(
        Guid quotationId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _quotations.ReviseAsync(
            quotationId,
            currentUserId,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpPatch("quotations/{quotationId:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        Guid quotationId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _quotations.CancelAsync(
            quotationId,
            currentUserId,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER")]
    [HttpPatch("quotations/{quotationId:guid}/reject")]
    public async Task<IActionResult> Reject(
        Guid quotationId,
        [FromBody] RejectQuotationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _quotations.RejectAsync(
            quotationId,
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
