#nullable enable

using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Financial;
using FurniSpace.Application.Interfaces.Financial;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Admin;

[Authorize(Roles = "ADMIN")]
[Route("admin/financial/discounts")]
public sealed class AdminFinancialDiscountController : BaseApiController
{
    private readonly IAdminFinancialDiscountService _discounts;

    public AdminFinancialDiscountController(IAdminFinancialDiscountService discounts)
    {
        _discounts = discounts;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] AdminFinancialDiscountSummaryQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _discounts.GetSummaryAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("projects")]
    public async Task<IActionResult> GetProjects(
        [FromQuery] AdminFinancialDiscountProjectsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _discounts.GetProjectsAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("orders/{orderId:guid}")]
    public async Task<IActionResult> GetOrderDetail(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var result = await _discounts.GetOrderDetailAsync(orderId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("trend")]
    public async Task<IActionResult> GetTrend(
        [FromQuery] AdminFinancialDiscountTrendQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _discounts.GetTrendAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("exceptions")]
    public async Task<IActionResult> GetExceptions(
        [FromQuery] AdminFinancialDiscountExceptionsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _discounts.GetExceptionsAsync(query, cancellationToken);
        return ToActionResult(result);
    }
}
