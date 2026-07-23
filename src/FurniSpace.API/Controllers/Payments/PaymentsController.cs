#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Payments;

[Authorize]
[Route("api/payments")]
public sealed class PaymentsController : BaseApiController
{
    private readonly IPaymentService _payments;

    public PaymentsController(IPaymentService payments)
    {
        _payments = payments;
    }

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,ADMIN")]
    [HttpGet("{paymentId:guid}")]
    public async Task<IActionResult> GetById(Guid paymentId, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _payments.GetByIdAsync(paymentId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,ADMIN")]
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] Guid? projectId = null,
        [FromQuery] Guid? orderId = null,
        [FromQuery] PaymentStatus? status = null,
        [FromQuery] PaymentType? paymentType = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _payments.GetListAsync(
            currentUserId,
            new PaymentQueryDto
            {
                ProjectId = projectId,
                OrderId = orderId,
                Status = status,
                PaymentType = paymentType
            },
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,ADMIN")]
    [HttpGet("{paymentId:guid}/transactions")]
    public async Task<IActionResult> GetTransactions(Guid paymentId, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _payments.GetTransactionsAsync(paymentId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,ADMIN")]
    [HttpGet("code/{paymentCode}/status")]
    public async Task<IActionResult> GetStatusByCode(string paymentCode, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _payments.GetStatusByCodeAsync(paymentCode, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,ADMIN")]
    [HttpPost("{paymentId:guid}/sepay/vietqr")]
    public async Task<IActionResult> GenerateSePayVietQr(Guid paymentId, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _payments.GenerateSePayVietQrAsync(paymentId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,ADMIN")]
    [HttpPost("{paymentId:guid}/transactions")]
    public async Task<IActionResult> CreatePaymentTransactionAttempt(
        Guid paymentId,
        [FromBody] CreatePaymentTransactionAttemptRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _payments.CreatePaymentTransactionAttemptAsync(
            paymentId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,ADMIN")]
    [HttpPost("{paymentId:guid}/payos/payment-link")]
    public async Task<IActionResult> CreatePayOsPaymentLink(
        Guid paymentId,
        [FromBody] CreatePayOsPaymentLinkRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _payments.CreatePayOsPaymentLinkAsync(
            paymentId,
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
