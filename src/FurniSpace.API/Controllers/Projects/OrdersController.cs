#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.DTOs.Production;
using FurniSpace.Application.Interfaces.Orders;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Application.Interfaces.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Projects;

[Authorize]
[Route("")]
public sealed class OrdersController : BaseApiController
{
    private readonly IOrderService _orders;
    private readonly IPaymentService _payments;
    private readonly IProductionRequestService _productionRequests;

    public OrdersController(
        IOrderService orders,
        IPaymentService payments,
        IProductionRequestService productionRequests)
    {
        _orders = orders;
        _payments = payments;
        _productionRequests = productionRequests;
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

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpPatch("orders/{orderId:guid}/financial-adjustment")]
    public async Task<IActionResult> UpdateFinancialAdjustment(
        Guid orderId,
        [FromBody] UpdateOrderFinancialAdjustmentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _orders.UpdateFinancialAdjustmentAsync(
            orderId,
            currentUserId,
            request,
            cancellationToken);
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

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpPost("orders/{orderId:guid}/production-request")]
    public async Task<IActionResult> CreateProductionRequest(
        Guid orderId,
        [FromBody] CreateProductionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _productionRequests.CreateAsync(
            orderId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpPost("orders/{orderId:guid}/adjustments")]
    public async Task<IActionResult> CreateAdjustment(
        Guid orderId,
        [FromBody] CreateOrderAdjustmentDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _orders.CreateAdjustmentAsync(
            orderId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpPost("order-adjustments/{orderAdjustmentId:guid}/items")]
    public async Task<IActionResult> AddAdjustmentItem(
        Guid orderAdjustmentId,
        [FromBody] UpsertOrderAdjustmentItemDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _orders.AddAdjustmentItemAsync(
            orderAdjustmentId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpPatch("order-adjustment-items/{orderAdjustmentItemId:guid}")]
    public async Task<IActionResult> UpdateAdjustmentItem(
        Guid orderAdjustmentItemId,
        [FromBody] UpsertOrderAdjustmentItemDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _orders.UpdateAdjustmentItemAsync(
            orderAdjustmentItemId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpDelete("order-adjustment-items/{orderAdjustmentItemId:guid}")]
    public async Task<IActionResult> DeleteAdjustmentItem(
        Guid orderAdjustmentItemId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _orders.DeleteAdjustmentItemAsync(
            orderAdjustmentItemId,
            currentUserId,
            cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
