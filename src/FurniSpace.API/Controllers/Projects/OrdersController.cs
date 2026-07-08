#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.Interfaces.Orders;
using FurniSpace.Application.Interfaces.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Projects;

[Authorize]
[Route("")]
public sealed class OrdersController : BaseApiController
{
    private readonly IOrderService _orders;
    private readonly IPaymentService _payments;

    public OrdersController(IOrderService orders, IPaymentService payments)
    {
        _orders = orders;
        _payments = payments;
    }

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,PRODUCTION,ADMIN")]
    [HttpGet("projects/{projectId:guid}/orders")]
    public async Task<IActionResult> GetByProject(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _orders.GetByProjectAsync(projectId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,PRODUCTION,ADMIN")]
    [HttpGet("orders/{orderId:guid}")]
    public async Task<IActionResult> GetDetail(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _orders.GetDetailAsync(orderId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,ADMIN")]
    [HttpPost("orders/{orderId:guid}/payments/deposit")]
    public async Task<IActionResult> CreateDepositPayment(
        Guid orderId,
        [FromBody] CreateOrderDepositPaymentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _payments.CreateDepositPaymentForOrderAsync(
            orderId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,ADMIN")]
    [HttpPost("orders/{orderId:guid}/payments/remaining")]
    public async Task<IActionResult> CreateRemainingPayment(
        Guid orderId,
        [FromBody] CreateOrderRemainingPaymentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _payments.CreateRemainingPaymentForOrderAsync(
            orderId,
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
