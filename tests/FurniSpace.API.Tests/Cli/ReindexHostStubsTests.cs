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

        var sendToUser = service.SendToUserAsync(Guid.NewGuid(), "event", new { Value = 1 });
        var sendToRole = service.SendToRoleAsync("ADMIN", "event", new { Value = 1 });
        var sendToUsers = service.SendToUsersAsync([Guid.NewGuid(), Guid.NewGuid()], "event", new { Value = 1 });

        await Task.WhenAll(sendToUser, sendToRole, sendToUsers);

        Assert.All([sendToUser, sendToRole, sendToUsers], task => Assert.True(task.IsCompletedSuccessfully));
    }

    [Fact]
    public async Task NoOpProjectChatRealtimeService_CompletesMessageSent()
    {
        var service = new NoOpProjectChatRealtimeService();

        var sendMessage = service.SendMessageSentAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ProjectChatMessageDto { MessageId = Guid.NewGuid() });

        await sendMessage;

        Assert.True(sendMessage.IsCompletedSuccessfully);
    }
}
