#nullable enable

using System.Security.Claims;
using FurniSpace.Application.Common.Realtime;
using FurniSpace.Application.Interfaces.ProjectChatMessages;
using FurniSpace.Application.Interfaces.ProjectChats;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FurniSpace.API.Hubs;

[Authorize]
public sealed class ProjectChatHub : Hub
{
    private readonly IProjectChatMessageService _messages;
    private readonly IProjectChatService _projectChats;

    public ProjectChatHub(
        IProjectChatService projectChats,
        IProjectChatMessageService messages)
    {
        _projectChats = projectChats;
        _messages = messages;
    }

    public override async Task OnConnectedAsync()
    {
        if (TryGetCurrentUserId(out var currentUserId))
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                RealtimeGroupNames.User(currentUserId));
        }

        await base.OnConnectedAsync();
    }

    public async Task JoinProject(Guid projectId)
    {
        var currentUserId = GetCurrentUserId();
        if (!await _projectChats.CanAccessProjectAsync(projectId, currentUserId, Context.ConnectionAborted))
        {
            throw new HubException("You do not have access to this project.");
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            ProjectChatRealtimeConstants.Project(projectId),
            Context.ConnectionAborted);
    }

    public async Task JoinChat(Guid chatId)
    {
        var currentUserId = GetCurrentUserId();
        if (!await _messages.CanAccessChatAsync(chatId, currentUserId, Context.ConnectionAborted))
        {
            throw new HubException("You do not have access to this project chat.");
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            ProjectChatRealtimeConstants.Chat(chatId),
            Context.ConnectionAborted);
    }

    public Task LeaveProject(Guid projectId)
    {
        return Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            ProjectChatRealtimeConstants.Project(projectId),
            Context.ConnectionAborted);
    }

    public Task LeaveChat(Guid chatId)
    {
        return Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            ProjectChatRealtimeConstants.Chat(chatId),
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
