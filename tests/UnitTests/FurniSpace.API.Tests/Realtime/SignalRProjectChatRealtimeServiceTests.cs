#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Hubs;
using FurniSpace.API.Realtime;
using FurniSpace.Application.Common.Realtime;
using FurniSpace.Application.DTOs.ProjectChatMessages;
using Microsoft.AspNetCore.SignalR;
using Xunit;

namespace FurniSpace.API.Tests.Realtime;

public sealed class SignalRProjectChatRealtimeServiceTests
{
    [Fact]
    public async Task SendMessageSentAsync_SendsExpectedEventToChatGroup()
    {
        var projectId = Guid.NewGuid();
        var chatId = Guid.NewGuid();
        var message = new ProjectChatMessageDto
        {
            MessageId = Guid.NewGuid(),
            ChatId = chatId,
            Content = "Hello"
        };
        var clients = new FakeHubClients();
        var service = new SignalRProjectChatRealtimeService(
            new FakeHubContext(clients));

        await service.SendMessageSentAsync(projectId, chatId, message);

        Assert.Equal(ProjectChatRealtimeConstants.Chat(chatId), clients.GroupName);
        Assert.Equal(ProjectChatRealtimeConstants.MessageSentEvent, clients.Proxy.Method);
        var payload = Assert.Single(clients.Proxy.Arguments!);
        var payloadType = payload!.GetType();
        Assert.Equal(projectId, payloadType.GetProperty("projectId")!.GetValue(payload));
        Assert.Equal(chatId, payloadType.GetProperty("chatId")!.GetValue(payload));
        Assert.Same(message, payloadType.GetProperty("message")!.GetValue(payload));
    }

    private sealed class FakeHubContext : IHubContext<ProjectChatHub>
    {
        public FakeHubContext(IHubClients clients)
        {
            Clients = clients;
        }

        public IHubClients Clients { get; }
        public IGroupManager Groups { get; } = new NoOpGroupManager();
    }

    private sealed class FakeHubClients : IHubClients
    {
        public string? GroupName { get; private set; }
        public FakeClientProxy Proxy { get; } = new();
        public IClientProxy All => Proxy;

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy;
        public IClientProxy Client(string connectionId) => Proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy;

        public IClientProxy Group(string groupName)
        {
            GroupName = groupName;
            return Proxy;
        }

        public IClientProxy GroupExcept(
            string groupName,
            IReadOnlyList<string> excludedConnectionIds) => Proxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy;
        public IClientProxy User(string userId) => Proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy;
    }

    private sealed class FakeClientProxy : IClientProxy
    {
        public string? Method { get; private set; }
        public object?[]? Arguments { get; private set; }

        public Task SendCoreAsync(
            string method,
            object?[] args,
            CancellationToken cancellationToken = default)
        {
            Method = method;
            Arguments = args;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveFromGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
