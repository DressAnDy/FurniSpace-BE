#nullable enable

using System;
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

    [Authorize(Roles = "SALES,ADMIN")]
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

    [Authorize(Roles = "ADMIN")]
    [Obsolete("Use POST /orders/{orderId}/deliveries with a confirmed delivery schedule instead.")]
    [HttpPatch("orders/{orderId:guid}/prepare-final-payment")]
    public async Task<IActionResult> PrepareFinalPayment(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _orders.PrepareFinalPaymentAsync(
            orderId,
            currentUserId,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpPatch("orders/{orderId:guid}/complete")]
    public async Task<IActionResult> Complete(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _orders.CompleteAsync(
            orderId,
            currentUserId,
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

    [Authorize(Roles = "ADMIN")]
    [Obsolete("Use POST /orders/{orderId}/deliveries with a confirmed delivery schedule instead.")]
    [HttpPatch("orders/{orderId:guid}/start-delivery")]
    public async Task<IActionResult> StartDelivery(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _orders.StartDeliveryAsync(
            orderId,
            currentUserId,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [Obsolete("Use POST /orders/{orderId}/deliveries with a confirmed delivery schedule instead.")]
    [HttpPatch("orders/{orderId:guid}/complete-delivery")]
    public async Task<IActionResult> CompleteDelivery(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _orders.CompleteDeliveryAsync(
            orderId,
            currentUserId,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER")]
    [HttpPatch("orders/{orderId:guid}/confirm-delivery")]
    public async Task<IActionResult> ConfirmDelivery(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _orders.ConfirmDeliveryAsync(
            orderId,
            currentUserId,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,PRODUCTION,ADMIN")]
    [HttpGet("orders/{orderId:guid}/deliveries")]
    public async Task<IActionResult> GetDeliveries(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _orders.GetDeliveriesAsync(orderId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,PRODUCTION,ADMIN")]
    [HttpGet("orders/{orderId:guid}/deliveries/{deliveryId:guid}")]
    public async Task<IActionResult> GetDeliveryDetail(
        Guid orderId,
        Guid deliveryId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _orders.GetDeliveryDetailAsync(
            orderId,
            deliveryId,
            currentUserId,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "PRODUCTION,ADMIN")]
    [HttpPost("orders/{orderId:guid}/deliveries")]
    public async Task<IActionResult> CreateDeliveryBatch(
        Guid orderId,
        [FromBody] CreateDeliveryBatchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _orders.CreateDeliveryBatchAsync(
            orderId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "PRODUCTION,ADMIN")]
    [HttpPatch("orders/{orderId:guid}/deliveries/{deliveryId:guid}/complete")]
    public async Task<IActionResult> CompleteDeliveryBatch(
        Guid orderId,
        Guid deliveryId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _orders.CompleteDeliveryBatchAsync(
            orderId,
            deliveryId,
            currentUserId,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,PRODUCTION,ADMIN")]
    [HttpGet("orders/{orderId:guid}/delivery-tracking")]
    public async Task<IActionResult> GetDeliveryTracking(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _orders.GetDeliveryTrackingAsync(orderId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
