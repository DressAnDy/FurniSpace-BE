#nullable enable

using System.Security.Claims;
using FurniSpace.Application.Common.Realtime;
using FurniSpace.Application.Interfaces.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FurniSpace.API.Hubs;

[Authorize(Roles = "CUSTOMER,SALES,DESIGNER,ADMIN")]
public sealed class PaymentHub : Hub
{
    private readonly IPaymentService _payments;

    public PaymentHub(IPaymentService payments)
    {
        _payments = payments;
    }

    public async Task JoinPayment(Guid paymentId)
    {
        var currentUserId = GetCurrentUserId();
        if (!await _payments.CanAccessPaymentAsync(paymentId, currentUserId, Context.ConnectionAborted))
        {
            throw new HubException("You do not have access to this payment.");
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            PaymentRealtimeConstants.Payment(paymentId),
            Context.ConnectionAborted);
    }

    public Task LeavePayment(Guid paymentId)
    {
        return Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            PaymentRealtimeConstants.Payment(paymentId),
            Context.ConnectionAborted);
    }

    private Guid GetCurrentUserId()
    {
        return TryGetCurrentUserId(out var currentUserId)
            ? currentUserId
            : throw new HubException("Authenticated account id is required.");
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(
            Context.User?.FindFirstValue(ClaimTypes.NameIdentifier),
            out currentUserId);
    }
}
