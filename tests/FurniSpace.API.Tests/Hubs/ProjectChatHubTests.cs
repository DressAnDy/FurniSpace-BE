#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Hubs;
using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Realtime;
using FurniSpace.Application.DTOs.ProjectChatMessages;
using FurniSpace.Application.DTOs.ProjectChats;
using FurniSpace.Application.Interfaces.ProjectChatMessages;
using FurniSpace.Application.Interfaces.ProjectChats;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Xunit;

namespace FurniSpace.API.Tests.Hubs;

public sealed class ProjectChatHubTests
{
    [Fact]
    public void Hub_RequiresAuthentication()
    {
        var authorize = typeof(ProjectChatHub)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(authorize);
    }

    [Fact]
    public async Task OnConnectedAsync_WithAccountId_AddsUserGroup()
    {
        var accountId = Guid.NewGuid();
        var groups = new FakeGroupManager();
        var hub = BuildHub(groups, accountId: accountId);

        await hub.OnConnectedAsync();

        var added = Assert.Single(groups.AddedGroups);
        Assert.Equal("connection-1", added.ConnectionId);
        Assert.Equal(RealtimeGroupNames.User(accountId), added.GroupName);
    }

    [Fact]
    public async Task OnConnectedAsync_WithoutAccountId_DoesNotAddUserGroup()
    {
        var groups = new FakeGroupManager();
        var hub = BuildHub(groups);

        await hub.OnConnectedAsync();

        Assert.Empty(groups.AddedGroups);
    }

    [Fact]
    public async Task JoinProject_WithAccess_AddsProjectGroup()
    {
        var accountId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var groups = new FakeGroupManager();
        var projectChats = new FakeProjectChatService { CanAccess = true };
        var hub = BuildHub(groups, accountId, projectChats);

        await hub.JoinProject(projectId);

        var added = Assert.Single(groups.AddedGroups);
        Assert.Equal(ProjectChatRealtimeConstants.Project(projectId), added.GroupName);
        Assert.Equal(projectId, projectChats.ProjectId);
        Assert.Equal(accountId, projectChats.CurrentUserId);
    }

    [Fact]
    public async Task JoinProject_WithoutAccess_ThrowsHubException()
    {
        var hub = BuildHub(
            new FakeGroupManager(),
            Guid.NewGuid(),
            new FakeProjectChatService { CanAccess = false });

        var exception = await Assert.ThrowsAsync<HubException>(() => hub.JoinProject(Guid.NewGuid()));

        Assert.Equal("You do not have access to this project.", exception.Message);
    }

    [Fact]
    public async Task JoinChat_WithAccess_AddsChatGroup()
    {
        var accountId = Guid.NewGuid();
        var chatId = Guid.NewGuid();
        var groups = new FakeGroupManager();
        var messages = new FakeProjectChatMessageService { CanAccess = true };
        var hub = BuildHub(groups, accountId, messages: messages);

        await hub.JoinChat(chatId);

        var added = Assert.Single(groups.AddedGroups);
        Assert.Equal(ProjectChatRealtimeConstants.Chat(chatId), added.GroupName);
        Assert.Equal(chatId, messages.ChatId);
        Assert.Equal(accountId, messages.CurrentUserId);
    }

    [Fact]
    public async Task JoinChat_WithoutAccess_ThrowsHubException()
    {
        var hub = BuildHub(
            new FakeGroupManager(),
            Guid.NewGuid(),
            messages: new FakeProjectChatMessageService { CanAccess = false });

        var exception = await Assert.ThrowsAsync<HubException>(() => hub.JoinChat(Guid.NewGuid()));

        Assert.Equal("You do not have access to this project chat.", exception.Message);
    }

    [Fact]
    public async Task JoinGroup_WithoutAccountClaim_ThrowsHubException()
    {
        var hub = BuildHub(new FakeGroupManager());

        var exception = await Assert.ThrowsAsync<HubException>(() => hub.JoinChat(Guid.NewGuid()));

        Assert.Equal("Authenticated account id is required.", exception.Message);
    }

    [Fact]
    public async Task LeaveMethods_RemoveExpectedGroups()
    {
        var projectId = Guid.NewGuid();
        var chatId = Guid.NewGuid();
        var groups = new FakeGroupManager();
        var hub = BuildHub(groups, Guid.NewGuid());

        await hub.LeaveProject(projectId);
        await hub.LeaveChat(chatId);

        Assert.Collection(
            groups.RemovedGroups,
            removed => Assert.Equal(ProjectChatRealtimeConstants.Project(projectId), removed.GroupName),
            removed => Assert.Equal(ProjectChatRealtimeConstants.Chat(chatId), removed.GroupName));
    }

    private static ProjectChatHub BuildHub(
        FakeGroupManager groups,
        Guid? accountId = null,
        FakeProjectChatService? projectChats = null,
        FakeProjectChatMessageService? messages = null)
    {
        var claims = accountId.HasValue
            ? new[] { new Claim(ClaimTypes.NameIdentifier, accountId.Value.ToString()) }
            : [];
        var hub = new ProjectChatHub(
            projectChats ?? new FakeProjectChatService(),
            messages ?? new FakeProjectChatMessageService())
        {
            Context = new FakeHubCallerContext(
                "connection-1",
                new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))),
            Groups = groups
        };

        return hub;
    }

    private sealed class FakeProjectChatService : IProjectChatService
    {
        public bool CanAccess { get; init; }
        public Guid ProjectId { get; private set; }
        public Guid CurrentUserId { get; private set; }

        public Task<bool> CanAccessProjectAsync(
            Guid projectId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            return Task.FromResult(CanAccess);
        }

        public Task<ServiceResult<ProjectChatSummaryDto>> CreateManualAsync(
            Guid projectId,
            Guid currentUserId,
            CreateProjectChatRequestDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<ProjectChatSummaryDto>.Created(new ProjectChatSummaryDto()));

        public Task<ServiceResult<ProjectChatListResponseDto>> GetProjectChatsAsync(
            Guid projectId,
            Guid currentUserId,
            ProjectChatListQueryDto query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<ProjectChatListResponseDto>.Success(new ProjectChatListResponseDto()));

        public Task<ProjectChatSummaryDto> UpsertProjectChatAsync(
            Guid projectId,
            ProjectChatType chatType,
            Guid staffId,
            string title,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProjectChatSummaryDto());

        public Task<ServiceResult<ProjectChatSummaryDto>> UpdateStatusAsync(
            Guid chatId,
            Guid currentUserId,
            UpdateProjectChatStatusRequestDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<ProjectChatSummaryDto>.Success(new ProjectChatSummaryDto()));
    }

    private sealed class FakeProjectChatMessageService : IProjectChatMessageService
    {
        public bool CanAccess { get; init; }
        public Guid ChatId { get; private set; }
        public Guid CurrentUserId { get; private set; }

        public Task<bool> CanAccessChatAsync(
            Guid chatId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            ChatId = chatId;
            CurrentUserId = currentUserId;
            return Task.FromResult(CanAccess);
        }

        public Task<ServiceResult<ProjectChatMessageListResponseDto>> GetMessagesAsync(
            Guid chatId,
            Guid currentUserId,
            ProjectChatMessageQueryDto query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<ProjectChatMessageListResponseDto>.Success(
                new ProjectChatMessageListResponseDto()));

        public Task<ServiceResult<ProjectChatMessageDto>> SendTextMessageAsync(
            Guid chatId,
            Guid currentUserId,
            SendTextChatMessageRequestDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<ProjectChatMessageDto>.Created(new ProjectChatMessageDto()));

        public Task<ServiceResult<ProjectChatMessageDto>> SendFileMessageAsync(
            Guid chatId,
            Guid currentUserId,
            SendFileChatMessageRequestDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<ProjectChatMessageDto>.Created(new ProjectChatMessageDto()));
    }

    private sealed class FakeGroupManager : IGroupManager
    {
        public List<(string ConnectionId, string GroupName)> AddedGroups { get; } = [];
        public List<(string ConnectionId, string GroupName)> RemovedGroups { get; } = [];

        public Task AddToGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default)
        {
            AddedGroups.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default)
        {
            RemovedGroups.Add((connectionId, groupName));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHubCallerContext : HubCallerContext
    {
        public FakeHubCallerContext(string connectionId, ClaimsPrincipal user)
        {
            ConnectionId = connectionId;
            User = user;
        }

        public override string ConnectionId { get; }
        public override string? UserIdentifier => User?.FindFirstValue(ClaimTypes.NameIdentifier);
        public override ClaimsPrincipal? User { get; }
        public override IDictionary<object, object?> Items { get; } =
            new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort()
        {
        }
    }
}
