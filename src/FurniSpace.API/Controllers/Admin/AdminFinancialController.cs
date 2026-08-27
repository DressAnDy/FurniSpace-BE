#nullable enable

using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Financial;
using FurniSpace.Application.Interfaces.Financial;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Admin;

[Route("admin/financial")]
public sealed class AdminFinancialController : BaseApiController
{
    private readonly IAdminFinancialService _financial;

    public AdminFinancialController(IAdminFinancialService financial)
    {
        _financial = financial;
    }

    [Authorize(Roles = "ADMIN")]
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] AdminFinancialSummaryQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _financial.GetSummaryAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpGet("receivables")]
    public async Task<IActionResult> GetReceivables(
        [FromQuery] AdminFinancialReceivablesQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _financial.GetReceivablesAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpGet("receivables/items")]
    public async Task<IActionResult> GetReceivableItems(
        [FromQuery] AdminFinancialReceivablesQueryDto query,
        CancellationToken cancellationToken = default)
    {
        return await GetReceivables(query, cancellationToken);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpGet("receivables/orders/{orderId:guid}")]
    public async Task<IActionResult> GetReceivableOrderDetail(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var result = await _financial.GetReceivableOrderDetailAsync(orderId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpGet("payment-breakdown")]
    public async Task<IActionResult> GetPaymentBreakdown(
        [FromQuery] AdminFinancialPaymentBreakdownQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _financial.GetPaymentBreakdownAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpGet("collection-trend")]
    public async Task<IActionResult> GetCollectionTrend(
        [FromQuery] AdminFinancialCollectionTrendQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _financial.GetCollectionTrendAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpGet("projects")]
    public async Task<IActionResult> GetProjects(
        [FromQuery] AdminFinancialProjectsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _financial.GetProjectsAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpGet("projects/{projectId:guid}")]
    public async Task<IActionResult> GetProject(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var result = await _financial.GetProjectAsync(projectId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpGet("projects/{projectId:guid}/statement")]
    public async Task<IActionResult> GetProjectStatement(
        Guid projectId,
        [FromQuery] AdminFinancialProjectStatementQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _financial.GetProjectStatementAsync(projectId, query, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpGet("payments")]
    public async Task<IActionResult> GetPayments(
        [FromQuery] AdminFinancialPaymentsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _financial.GetPaymentsAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpGet("exceptions")]
    public async Task<IActionResult> GetExceptions(
        [FromQuery] AdminFinancialExceptionsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _financial.GetExceptionsAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpGet("summary/{metric}/drilldown")]
    public async Task<IActionResult> GetSummaryDrilldown(
        string metric,
        [FromQuery] AdminFinancialSummaryDrilldownQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _financial.GetSummaryDrilldownAsync(metric, query, cancellationToken);
        return ToActionResult(result);
    }
}
