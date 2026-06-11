#nullable enable

using System.Security.Claims;
using FurniSpace.Application.Common.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FurniSpace.API.Hubs;

[Authorize]
public sealed class NotificationsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var accountIdValue = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(accountIdValue, out var accountId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroupNames.User(accountId));
        }

        var roleClaims = Context.User?.FindAll(ClaimTypes.Role) ?? [];
        foreach (var roleClaim in roleClaims)
        {
            if (string.IsNullOrWhiteSpace(roleClaim.Value))
            {
                continue;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroupNames.Role(roleClaim.Value));
        }

        await base.OnConnectedAsync();
    }
}
