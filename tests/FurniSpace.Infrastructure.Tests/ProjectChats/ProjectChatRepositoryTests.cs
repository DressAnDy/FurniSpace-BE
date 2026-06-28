#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.ProjectChats;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.ProjectChats;

public sealed class ProjectChatRepositoryTests
{
    [Fact]
    public async Task GetAccessAsync_WithExistingProject_ReturnsRoleAndAssignments()
    {
        var (context, data) = await ProjectChatTestDataFactory.CreateSeededContextAsync();
        await using (context)
        {
            var repository = ProjectChatTestDataFactory.CreateRepository(context);

            var access = await repository.GetAccessAsync(data.ProjectId, data.SalesAccountId);

            Assert.NotNull(access);
            Assert.Equal(data.ProjectId, access!.ProjectId);
            Assert.Equal(data.CustomerAccountId, access.CustomerId);
            Assert.Equal(data.SalesAccountId, access.AssignedSalesId);
            Assert.Equal("SALES", access.RoleName);
        }
    }

    [Fact]
    public async Task GetAccessAsync_WithMissingProject_ReturnsNull()
    {
        var (context, data) = await ProjectChatTestDataFactory.CreateSeededContextAsync();
        await using (context)
        {
            var repository = ProjectChatTestDataFactory.CreateRepository(context);

            var access = await repository.GetAccessAsync(Guid.NewGuid(), data.SalesAccountId);

            Assert.Null(access);
        }
    }

    [Fact]
    public async Task GetStatusAccessAsync_WithExistingChat_ReturnsChatAndRole()
    {
        var (context, data) = await ProjectChatTestDataFactory.CreateSeededContextAsync();
        await using (context)
        {
            var repository = ProjectChatTestDataFactory.CreateRepository(context);

            var access = await repository.GetStatusAccessAsync(data.SalesChatId, data.SalesAccountId);

            Assert.NotNull(access);
            Assert.Equal(data.SalesChatId, access!.ChatId);
            Assert.Equal(ProjectChatType.SALES, access.ChatType);
            Assert.Equal(ProjectChatStatus.OPEN, access.ChatStatus);
            Assert.Equal("SALES", access.RoleName);
        }
    }

    [Fact]
    public async Task GetStatusAccessAsync_WithMissingChat_ReturnsNull()
    {
        var (context, data) = await ProjectChatTestDataFactory.CreateSeededContextAsync();
        await using (context)
        {
            var repository = ProjectChatTestDataFactory.CreateRepository(context);

            var access = await repository.GetStatusAccessAsync(Guid.NewGuid(), data.SalesAccountId);

            Assert.Null(access);
        }
    }

    [Fact]
    public async Task GetListAsync_WithoutStatusFilter_ExcludesArchivedChats()
    {
        var (context, data) = await ProjectChatTestDataFactory.CreateSeededContextAsync();
        await using (context)
        {
            var repository = ProjectChatTestDataFactory.CreateRepository(context);

            var (items, total) = await repository.GetListAsync(
                data.ProjectId,
                new ProjectChatListQueryReadModel());

            Assert.Equal(2, total);
            Assert.DoesNotContain(items, item => item.ChatId == data.ArchivedChatId);
        }
    }

    [Fact]
    public async Task GetListAsync_WithStatusFilter_ReturnsMatchingChats()
    {
        var (context, data) = await ProjectChatTestDataFactory.CreateSeededContextAsync();
        await using (context)
        {
            var repository = ProjectChatTestDataFactory.CreateRepository(context);

            var (items, total) = await repository.GetListAsync(
                data.ProjectId,
                new ProjectChatListQueryReadModel { Status = ProjectChatStatus.ARCHIVED });

            Assert.Equal(1, total);
            Assert.Equal(data.ArchivedChatId, items[0].ChatId);
        }
    }

    [Fact]
    public async Task GetListAsync_WithChatTypeFilter_ReturnsMatchingChat()
    {
        var (context, data) = await ProjectChatTestDataFactory.CreateSeededContextAsync();
        await using (context)
        {
            var repository = ProjectChatTestDataFactory.CreateRepository(context);

            var (items, total) = await repository.GetListAsync(
                data.ProjectId,
                new ProjectChatListQueryReadModel { ChatType = ProjectChatType.DESIGNER });

            Assert.Equal(1, total);
            Assert.Equal(data.DesignerChatId, items[0].ChatId);
            Assert.Equal("Designer User", items[0].StaffName);
        }
    }

    [Fact]
    public async Task GetListAsync_WithAllowedChatTypes_FiltersByType()
    {
        var (context, data) = await ProjectChatTestDataFactory.CreateSeededContextAsync();
        await using (context)
        {
            var repository = ProjectChatTestDataFactory.CreateRepository(context);

            var (items, total) = await repository.GetListAsync(
                data.ProjectId,
                new ProjectChatListQueryReadModel
                {
                    AllowedChatTypes = [ProjectChatType.SALES, ProjectChatType.DESIGNER]
                });

            Assert.Equal(2, total);
            Assert.All(items, item =>
                Assert.Contains(item.ChatType, new[] { ProjectChatType.SALES, ProjectChatType.DESIGNER }));
        }
    }

    [Fact]
    public async Task GetListAsync_IncludesLastMessageWithPreviewTruncation()
    {
        var (context, data) = await ProjectChatTestDataFactory.CreateSeededContextAsync();
        await using (context)
        {
            var repository = ProjectChatTestDataFactory.CreateRepository(context);

            var (items, _) = await repository.GetListAsync(
                data.ProjectId,
                new ProjectChatListQueryReadModel { ChatType = ProjectChatType.SALES });

            var salesChat = Assert.Single(items);
            Assert.NotNull(salesChat.LastMessage);
            Assert.Equal(ProjectChatMessageType.FILE, salesChat.LastMessage!.MessageType);
            Assert.Equal("Attached file", salesChat.LastMessage.ContentPreview);
            Assert.Equal("Sales User", salesChat.LastMessage.SenderName);
        }
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsLatestNonArchivedChat()
    {
        var (context, data) = await ProjectChatTestDataFactory.CreateSeededContextAsync();
        await using (context)
        {
            var repository = ProjectChatTestDataFactory.CreateRepository(context);

            var chat = await repository.GetActiveAsync(data.ProjectId, ProjectChatType.SALES);

            Assert.NotNull(chat);
            Assert.Equal(data.SalesChatId, chat!.ChatId);
        }
    }

    [Fact]
    public async Task GetActiveAsync_WithArchivedType_ReturnsNull()
    {
        var (context, data) = await ProjectChatTestDataFactory.CreateSeededContextAsync();
        await using (context)
        {
            var repository = ProjectChatTestDataFactory.CreateRepository(context);

            var chat = await repository.GetActiveAsync(data.ProjectId, ProjectChatType.GENERAL);

            Assert.Null(chat);
        }
    }
}
