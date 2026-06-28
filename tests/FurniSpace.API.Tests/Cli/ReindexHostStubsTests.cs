#nullable enable

using System;
using System.Threading.Tasks;
using FurniSpace.API.Cli;
using FurniSpace.Application.DTOs.ProjectChatMessages;
using Xunit;

namespace FurniSpace.API.Tests.Cli;

public sealed class ReindexHostStubsTests
{
    [Fact]
    public async Task NoOpRealtimeNotificationService_CompletesAllSendMethods()
    {
        var service = new NoOpRealtimeNotificationService();

        await service.SendToUserAsync(Guid.NewGuid(), "event", new { Value = 1 });
        await service.SendToRoleAsync("ADMIN", "event", new { Value = 1 });
        await service.SendToUsersAsync([Guid.NewGuid(), Guid.NewGuid()], "event", new { Value = 1 });
    }

    [Fact]
    public async Task NoOpProjectChatRealtimeService_CompletesMessageSent()
    {
        var service = new NoOpProjectChatRealtimeService();

        await service.SendMessageSentAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ProjectChatMessageDto { MessageId = Guid.NewGuid() });
    }
}
