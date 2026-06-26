#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.ProjectChatMessages;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.ProjectChats;

public sealed class ProjectChatMessageRepositoryTests
{
    [Fact]
    public async Task GetAccessAsync_WithExistingChat_ReturnsAccessDetails()
    {
        var (context, data) = await ProjectChatTestDataFactory.CreateSeededContextAsync();
        await using (context)
        {
            var repository = ProjectChatTestDataFactory.CreateMessageRepository(context);

            var access = await repository.GetAccessAsync(data.SalesChatId, data.SalesAccountId);

            Assert.NotNull(access);
            Assert.Equal(data.SalesChatId, access!.ChatId);
            Assert.Equal(data.ProjectId, access.ProjectId);
            Assert.Equal(ProjectChatType.SALES, access.ChatType);
            Assert.Equal(ProjectChatStatus.OPEN, access.ChatStatus);
            Assert.Equal("SALES", access.RoleName);
            Assert.Equal("Sales User", access.CurrentUserName);
        }
    }

    [Fact]
    public async Task GetAccessAsync_WithMissingChat_ReturnsNull()
    {
        var (context, data) = await ProjectChatTestDataFactory.CreateSeededContextAsync();
        await using (context)
        {
            var repository = ProjectChatTestDataFactory.CreateMessageRepository(context);

            var access = await repository.GetAccessAsync(Guid.NewGuid(), data.SalesAccountId);

            Assert.Null(access);
        }
    }

    [Fact]
    public async Task GetMessagesAsync_WithDescendingSort_ReturnsNewestFirst()
    {
        var (context, data) = await ProjectChatTestDataFactory.CreateSeededContextAsync();
        await using (context)
        {
            var repository = ProjectChatTestDataFactory.CreateMessageRepository(context);

            var (items, total) = await repository.GetMessagesAsync(
                data.SalesChatId,
                new ProjectChatMessageQueryReadModel { Page = 1, Limit = 10, SortDescending = true });

            Assert.Equal(3, total);
            Assert.True(items[0].DeletedAt.HasValue);
            Assert.Null(items[0].Content);
        }
    }

    [Fact]
    public async Task GetMessagesAsync_WithAscendingSort_ReturnsOldestFirst()
    {
        var (context, data) = await ProjectChatTestDataFactory.CreateSeededContextAsync();
        await using (context)
        {
            var repository = ProjectChatTestDataFactory.CreateMessageRepository(context);

            var (items, total) = await repository.GetMessagesAsync(
                data.SalesChatId,
                new ProjectChatMessageQueryReadModel { Page = 1, Limit = 10, SortDescending = false });

            Assert.Equal(3, total);
            Assert.Equal(250, items[0].Content!.Length);
        }
    }

    [Fact]
    public async Task GetMessagesAsync_WithDeletedMessage_ClearsContentAndAttachment()
    {
        var (context, data) = await ProjectChatTestDataFactory.CreateSeededContextAsync();
        await using (context)
        {
            var repository = ProjectChatTestDataFactory.CreateMessageRepository(context);

            var (items, _) = await repository.GetMessagesAsync(
                data.SalesChatId,
                new ProjectChatMessageQueryReadModel { Page = 1, Limit = 10, SortDescending = true });

            var deleted = items.Single(item => item.DeletedAt.HasValue);
            Assert.Null(deleted.Content);
            Assert.Null(deleted.Attachment);
        }
    }

    [Fact]
    public async Task GetMessagesAsync_WithFileMessage_IncludesAttachmentMetadata()
    {
        var (context, data) = await ProjectChatTestDataFactory.CreateSeededContextAsync();
        await using (context)
        {
            var repository = ProjectChatTestDataFactory.CreateMessageRepository(context);

            var (items, _) = await repository.GetMessagesAsync(
                data.SalesChatId,
                new ProjectChatMessageQueryReadModel { Page = 1, Limit = 10, SortDescending = true });

            var fileMessage = items.Single(item => item.MessageId == data.FileMessageId);
            Assert.NotNull(fileMessage.Attachment);
            Assert.Equal(data.AttachmentFileId, fileMessage.Attachment!.FileId);
            Assert.Equal("floor-plan.pdf", fileMessage.Attachment.OriginalFileName);
            Assert.Equal("Sales User", fileMessage.SenderName);
            Assert.Equal("SALES", fileMessage.SenderRole);
        }
    }

    [Fact]
    public async Task GetMessagesAsync_WithPagination_ReturnsRequestedPage()
    {
        var (context, data) = await ProjectChatTestDataFactory.CreateSeededContextAsync();
        await using (context)
        {
            var repository = ProjectChatTestDataFactory.CreateMessageRepository(context);

            var (items, total) = await repository.GetMessagesAsync(
                data.SalesChatId,
                new ProjectChatMessageQueryReadModel
                {
                    Page = 2,
                    Limit = 1,
                    SortDescending = true
                });

            Assert.Equal(3, total);
            Assert.Single(items);
        }
    }
}
