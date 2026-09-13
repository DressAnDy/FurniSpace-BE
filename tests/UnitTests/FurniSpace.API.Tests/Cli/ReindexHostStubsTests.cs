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
    public void RedisConnectionMasker_WithEmptyValue_ReturnsInput()
    {
        Assert.Equal(string.Empty, RedisConnectionMasker.Mask(string.Empty));
        Assert.Null(RedisConnectionMasker.Mask(null!));
    }

    [Fact]
    public void RedisConnectionMasker_WithPasswordSegment_MasksCredential()
    {
        var masked = RedisConnectionMasker.Mask("host=localhost:6379,password=secret123,ssl=true");

        Assert.Contains("password=***", masked, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret123", masked, StringComparison.Ordinal);
        Assert.Contains("host=localhost:6379", masked, StringComparison.Ordinal);
    }

    [Fact]
    public void RedisConnectionMasker_WithUpperCasePasswordSegment_MasksCredential()
    {
        var masked = RedisConnectionMasker.Mask("PASSWORD=abc123,host=localhost");

        Assert.Contains("PASSWORD=***", masked, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("abc123", masked, StringComparison.Ordinal);
    }

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
