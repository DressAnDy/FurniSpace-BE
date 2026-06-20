#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.ProjectChats;
using FurniSpace.Application.Services.ProjectChats;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Infrastructure.DTOs.ProjectChats;
using Xunit;

namespace FurniSpace.Application.Tests.ProjectChats;

public sealed class ProjectChatServiceTests
{
    [Fact]
    public async Task GetProjectChatsAsync_WithAdmin_ReturnsClosedChatAndLastMessage()
    {
        var projectId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var closedChat = CreateListItem(projectId, ProjectChatType.SALES, ProjectChatStatus.CLOSED);
        closedChat.LastMessage = new ProjectChatLastMessageReadModel
        {
            MessageId = Guid.NewGuid(),
            SenderId = Guid.NewGuid(),
            SenderName = "Nguyen Van A",
            MessageType = null,
            ContentPreview = "Please provide the floor plan.",
            CreatedAt = DateTime.UtcNow
        };
        var archivedChat = CreateListItem(projectId, ProjectChatType.INTERNAL, ProjectChatStatus.ARCHIVED);
        var repository = new FakeProjectChatRepository(
            access: CreateAccess(projectId, adminId, "ADMIN"),
            listItems: [closedChat, archivedChat]);
        var service = new ProjectChatService(repository);

        var result = await service.GetProjectChatsAsync(
            projectId,
            adminId,
            new ProjectChatListQueryDto());

        Assert.Equal(200, result.Status);
        Assert.Equal("Project chats retrieved successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.Page);
        Assert.Equal(20, result.Data.Limit);
        Assert.Equal(1, result.Data.Total);
        var item = Assert.Single(result.Data.Items);
        Assert.Equal(closedChat.ChatId, item.ChatId);
        Assert.Equal(ProjectChatType.SALES.ToString(), item.ChatType);
        Assert.Equal(ProjectChatStatus.CLOSED.ToString(), item.Status);
        Assert.NotNull(item.LastMessage);
        Assert.Equal(ProjectChatMessageType.TEXT.ToString(), item.LastMessage.MessageType);
        Assert.Equal("Please provide the floor plan.", item.LastMessage.ContentPreview);
        Assert.Null(repository.LastListQuery!.AllowedChatTypes);
        Assert.Equal(1, repository.GetAccessCallCount);
        Assert.Equal(1, repository.GetListCallCount);
    }

    [Fact]
    public async Task GetProjectChatsAsync_WithOwnerCustomer_HidesInternalChat()
    {
        var projectId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var repository = new FakeProjectChatRepository(
            access: CreateAccess(projectId, customerId, "CUSTOMER"),
            listItems:
            [
                CreateListItem(projectId, ProjectChatType.SALES, ProjectChatStatus.OPEN),
                CreateListItem(projectId, ProjectChatType.DESIGNER, ProjectChatStatus.CLOSED),
                CreateListItem(projectId, ProjectChatType.INTERNAL, ProjectChatStatus.OPEN)
            ]);
        var service = new ProjectChatService(repository);

        var result = await service.GetProjectChatsAsync(
            projectId,
            customerId,
            new ProjectChatListQueryDto());

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Total);
        Assert.DoesNotContain(result.Data.Items, item => item.ChatType == ProjectChatType.INTERNAL.ToString());
        Assert.Equal(
            [ProjectChatType.SALES, ProjectChatType.DESIGNER],
            repository.LastListQuery!.AllowedChatTypes);
    }

    [Fact]
    public async Task GetProjectChatsAsync_WithAssignedSales_ReturnsSalesAndDesignerChats()
    {
        var projectId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var repository = new FakeProjectChatRepository(
            access: CreateAccess(projectId, salesId, "SALES"),
            listItems:
            [
                CreateListItem(projectId, ProjectChatType.SALES, ProjectChatStatus.OPEN),
                CreateListItem(projectId, ProjectChatType.DESIGNER, ProjectChatStatus.OPEN),
                CreateListItem(projectId, ProjectChatType.GENERAL, ProjectChatStatus.OPEN)
            ]);
        var service = new ProjectChatService(repository);

        var result = await service.GetProjectChatsAsync(
            projectId,
            salesId,
            new ProjectChatListQueryDto());

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Total);
        Assert.All(result.Data.Items, item =>
            Assert.Contains(item.ChatType, new[] { "SALES", "DESIGNER" }));
    }

    [Fact]
    public async Task GetProjectChatsAsync_WithAssignedDesigner_ReturnsOnlyDesignerChat()
    {
        var projectId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var repository = new FakeProjectChatRepository(
            access: CreateAccess(projectId, designerId, "DESIGNER"),
            listItems:
            [
                CreateListItem(projectId, ProjectChatType.SALES, ProjectChatStatus.OPEN),
                CreateListItem(projectId, ProjectChatType.DESIGNER, ProjectChatStatus.OPEN)
            ]);
        var service = new ProjectChatService(repository);

        var result = await service.GetProjectChatsAsync(
            projectId,
            designerId,
            new ProjectChatListQueryDto());

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        var item = Assert.Single(result.Data.Items);
        Assert.Equal(ProjectChatType.DESIGNER.ToString(), item.ChatType);
        Assert.Equal([ProjectChatType.DESIGNER], repository.LastListQuery!.AllowedChatTypes);
    }

    [Fact]
    public async Task GetProjectChatsAsync_WithFilters_PassesFiltersAndPagination()
    {
        var projectId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var repository = new FakeProjectChatRepository(
            access: CreateAccess(projectId, adminId, "ADMIN"),
            listItems: [CreateListItem(projectId, ProjectChatType.DESIGNER, ProjectChatStatus.OPEN)]);
        var service = new ProjectChatService(repository);

        var result = await service.GetProjectChatsAsync(
            projectId,
            adminId,
            new ProjectChatListQueryDto
            {
                Status = ProjectChatStatus.OPEN,
                ChatType = ProjectChatType.DESIGNER,
                Page = 1,
                Limit = 10
            });

        Assert.Equal(200, result.Status);
        Assert.Equal(projectId, repository.LastListProjectId);
        Assert.Equal(ProjectChatStatus.OPEN, repository.LastListQuery!.Status);
        Assert.Equal(ProjectChatType.DESIGNER, repository.LastListQuery.ChatType);
        Assert.Equal(1, repository.LastListQuery.Page);
        Assert.Equal(10, repository.LastListQuery.Limit);
    }

    [Theory]
    [InlineData("CUSTOMER")]
    [InlineData("SALES")]
    [InlineData("DESIGNER")]
    [InlineData("UNKNOWN")]
    [InlineData(null)]
    public async Task GetProjectChatsAsync_WithoutProjectAccess_ReturnsForbidden(string? roleName)
    {
        var projectId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var repository = new FakeProjectChatRepository(
            access: new ProjectChatAccessReadModel
            {
                ProjectId = projectId,
                CustomerId = Guid.NewGuid(),
                AssignedSalesId = Guid.NewGuid(),
                AssignedDesignerId = Guid.NewGuid(),
                RoleName = roleName
            });
        var service = new ProjectChatService(repository);

        var result = await service.GetProjectChatsAsync(
            projectId,
            currentUserId,
            new ProjectChatListQueryDto());

        Assert.Equal(403, result.Status);
        Assert.Equal("You do not have access to view chats for this project.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetAccessCallCount);
        Assert.Equal(0, repository.GetListCallCount);
    }

    [Fact]
    public async Task GetProjectChatsAsync_WhenProjectDoesNotExist_ReturnsNotFound()
    {
        var repository = new FakeProjectChatRepository();
        var service = new ProjectChatService(repository);

        var result = await service.GetProjectChatsAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ProjectChatListQueryDto());

        Assert.Equal(404, result.Status);
        Assert.Equal("Project not found.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetAccessCallCount);
        Assert.Equal(0, repository.GetListCallCount);
    }

    [Fact]
    public async Task GetProjectChatsAsync_WithEmptyProjectId_ReturnsBadRequest()
    {
        var repository = new FakeProjectChatRepository();
        var service = new ProjectChatService(repository);

        var result = await service.GetProjectChatsAsync(
            Guid.Empty,
            Guid.NewGuid(),
            new ProjectChatListQueryDto());

        Assert.Equal(400, result.Status);
        Assert.Equal("Project id is required.", result.Message);
        Assert.Equal(0, repository.GetAccessCallCount);
    }

    [Fact]
    public async Task GetProjectChatsAsync_WithEmptyCurrentUser_ReturnsUnauthorized()
    {
        var repository = new FakeProjectChatRepository();
        var service = new ProjectChatService(repository);

        var result = await service.GetProjectChatsAsync(
            Guid.NewGuid(),
            Guid.Empty,
            new ProjectChatListQueryDto());

        Assert.Equal(401, result.Status);
        Assert.Equal("Authenticated account id is required.", result.Message);
        Assert.Equal(0, repository.GetAccessCallCount);
    }

    [Theory]
    [InlineData(0, 20, "Page must be greater than zero.")]
    [InlineData(1, 0, "Limit must be between 1 and 100.")]
    [InlineData(1, 101, "Limit must be between 1 and 100.")]
    public async Task GetProjectChatsAsync_WithInvalidPagination_ReturnsBadRequest(
        int page,
        int limit,
        string expectedMessage)
    {
        var repository = new FakeProjectChatRepository();
        var service = new ProjectChatService(repository);

        var result = await service.GetProjectChatsAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ProjectChatListQueryDto { Page = page, Limit = limit });

        Assert.Equal(400, result.Status);
        Assert.Equal(expectedMessage, result.Message);
        Assert.Equal(0, repository.GetAccessCallCount);
    }

    [Fact]
    public async Task GetProjectChatsAsync_WithInvalidStatus_ReturnsBadRequest()
    {
        var repository = new FakeProjectChatRepository();
        var service = new ProjectChatService(repository);

        var result = await service.GetProjectChatsAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ProjectChatListQueryDto { Status = (ProjectChatStatus)999 });

        Assert.Equal(400, result.Status);
        Assert.Equal("Project chat status is invalid.", result.Message);
        Assert.Equal(0, repository.GetAccessCallCount);
    }

    [Fact]
    public async Task GetProjectChatsAsync_WithInvalidChatType_ReturnsBadRequest()
    {
        var repository = new FakeProjectChatRepository();
        var service = new ProjectChatService(repository);

        var result = await service.GetProjectChatsAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ProjectChatListQueryDto { ChatType = (ProjectChatType)999 });

        Assert.Equal(400, result.Status);
        Assert.Equal("Project chat type is invalid.", result.Message);
        Assert.Equal(0, repository.GetAccessCallCount);
    }

    [Theory]
    [InlineData(ProjectChatType.SALES)]
    [InlineData(ProjectChatType.DESIGNER)]
    public async Task UpsertProjectChatAsync_WhenActiveChatDoesNotExist_AddsOpenChat(ProjectChatType chatType)
    {
        var projectId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var repository = new FakeProjectChatRepository();
        var service = new ProjectChatService(repository);

        var result = await service.UpsertProjectChatAsync(
            projectId,
            chatType,
            staffId,
            "  Project support  ");

        Assert.NotEqual(Guid.Empty, result.ChatId);
        Assert.Equal(projectId, result.ProjectId);
        Assert.Equal(chatType.ToString(), result.ChatType);
        Assert.Equal(staffId, result.StaffId);
        Assert.Equal("Project support", result.Title);
        Assert.Equal(ProjectChatStatus.OPEN.ToString(), result.Status);
        Assert.Equal(1, repository.GetActiveCallCount);
        Assert.Equal(1, repository.AddCallCount);
        Assert.Equal(0, repository.UpdateCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpsertProjectChatAsync_WhenOpenChatExists_UpdatesOnlyStaffAndKeepsMessages()
    {
        var projectId = Guid.NewGuid();
        var originalStaffId = Guid.NewGuid();
        var newStaffId = Guid.NewGuid();
        var chat = CreateChat(projectId, originalStaffId, ProjectChatStatus.OPEN);
        var message = new ProjectChatMessage
        {
            MessageId = Guid.NewGuid(),
            ChatId = chat.ChatId,
            Content = "Existing message"
        };
        var repository = new FakeProjectChatRepository([chat], [message]);
        var service = new ProjectChatService(repository);

        var result = await service.UpsertProjectChatAsync(
            projectId,
            ProjectChatType.SALES,
            newStaffId,
            "Replacement title");

        Assert.Equal(chat.ChatId, result.ChatId);
        Assert.Equal(newStaffId, result.StaffId);
        Assert.Equal("Original title", result.Title);
        Assert.Equal(newStaffId, chat.StaffId);
        Assert.Equal("Original title", chat.Title);
        Assert.Same(message, Assert.Single(repository.Messages));
        Assert.Equal("Existing message", message.Content);
        Assert.Equal(0, repository.AddCallCount);
        Assert.Equal(1, repository.UpdateCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpsertProjectChatAsync_WhenClosedChatExists_UpdatesExistingChat()
    {
        var projectId = Guid.NewGuid();
        var chat = CreateChat(projectId, Guid.NewGuid(), ProjectChatStatus.CLOSED);
        var repository = new FakeProjectChatRepository([chat]);
        var service = new ProjectChatService(repository);
        var newStaffId = Guid.NewGuid();

        var result = await service.UpsertProjectChatAsync(
            projectId,
            ProjectChatType.SALES,
            newStaffId,
            "Sales chat");

        Assert.Equal(chat.ChatId, result.ChatId);
        Assert.Equal(ProjectChatStatus.CLOSED.ToString(), result.Status);
        Assert.Equal(newStaffId, chat.StaffId);
        Assert.Equal(0, repository.AddCallCount);
        Assert.Equal(1, repository.UpdateCallCount);
    }

    [Fact]
    public async Task UpsertProjectChatAsync_WhenExistingChatStatusIsNull_ReturnsOpenStatusFallback()
    {
        var projectId = Guid.NewGuid();
        var chat = CreateChat(projectId, Guid.NewGuid(), ProjectChatStatus.OPEN);
        chat.Status = null;
        var repository = new FakeProjectChatRepository([chat]);
        var service = new ProjectChatService(repository);

        var result = await service.UpsertProjectChatAsync(
            projectId,
            ProjectChatType.SALES,
            Guid.NewGuid(),
            "Sales chat");

        Assert.Equal(chat.ChatId, result.ChatId);
        Assert.Equal(ProjectChatStatus.OPEN.ToString(), result.Status);
        Assert.Equal(1, repository.UpdateCallCount);
    }

    [Fact]
    public async Task UpsertProjectChatAsync_WhenOnlyArchivedChatExists_CreatesNewChat()
    {
        var projectId = Guid.NewGuid();
        var archivedChat = CreateChat(projectId, Guid.NewGuid(), ProjectChatStatus.ARCHIVED);
        var repository = new FakeProjectChatRepository([archivedChat]);
        var service = new ProjectChatService(repository);

        var result = await service.UpsertProjectChatAsync(
            projectId,
            ProjectChatType.SALES,
            Guid.NewGuid(),
            "New sales chat");

        Assert.NotEqual(archivedChat.ChatId, result.ChatId);
        Assert.Equal(ProjectChatStatus.ARCHIVED, archivedChat.Status);
        Assert.Equal(2, repository.Chats.Count);
        Assert.Equal(1, repository.AddCallCount);
        Assert.Equal(0, repository.UpdateCallCount);
    }

    [Fact]
    public async Task UpsertProjectChatAsync_WithInvalidChatType_ThrowsValidationError()
    {
        var repository = new FakeProjectChatRepository();
        var service = new ProjectChatService(repository);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.UpsertProjectChatAsync(
                Guid.NewGuid(),
                (ProjectChatType)999,
                Guid.NewGuid(),
                "Project chat"));

        Assert.Equal("chatType", exception.ParamName);
        Assert.Equal(0, repository.GetActiveCallCount);
    }

    [Fact]
    public async Task UpsertProjectChatAsync_WithEmptyProjectId_ThrowsValidationError()
    {
        var service = new ProjectChatService(new FakeProjectChatRepository());

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertProjectChatAsync(
                Guid.Empty,
                ProjectChatType.SALES,
                Guid.NewGuid(),
                "Project chat"));

        Assert.Equal("projectId", exception.ParamName);
    }

    [Fact]
    public async Task UpsertProjectChatAsync_WithEmptyStaffId_ThrowsValidationError()
    {
        var service = new ProjectChatService(new FakeProjectChatRepository());

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertProjectChatAsync(
                Guid.NewGuid(),
                ProjectChatType.SALES,
                Guid.Empty,
                "Project chat"));

        Assert.Equal("staffId", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task UpsertProjectChatAsync_WithMissingTitle_ThrowsValidationError(string? title)
    {
        var service = new ProjectChatService(new FakeProjectChatRepository());

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertProjectChatAsync(
                Guid.NewGuid(),
                ProjectChatType.SALES,
                Guid.NewGuid(),
                title!));

        Assert.Equal("title", exception.ParamName);
    }

    [Fact]
    public async Task UpsertProjectChatAsync_WithTooLongTitle_ThrowsValidationError()
    {
        var service = new ProjectChatService(new FakeProjectChatRepository());

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertProjectChatAsync(
                Guid.NewGuid(),
                ProjectChatType.SALES,
                Guid.NewGuid(),
                new string('T', 151)));

        Assert.Equal("title", exception.ParamName);
    }

    private static ProjectChatAccessReadModel CreateAccess(
        Guid projectId,
        Guid currentUserId,
        string roleName)
    {
        return new ProjectChatAccessReadModel
        {
            ProjectId = projectId,
            CustomerId = string.Equals(roleName, "CUSTOMER", StringComparison.OrdinalIgnoreCase)
                ? currentUserId
                : Guid.NewGuid(),
            AssignedSalesId = string.Equals(roleName, "SALES", StringComparison.OrdinalIgnoreCase)
                ? currentUserId
                : Guid.NewGuid(),
            AssignedDesignerId = string.Equals(roleName, "DESIGNER", StringComparison.OrdinalIgnoreCase)
                ? currentUserId
                : Guid.NewGuid(),
            RoleName = roleName
        };
    }

    private static ProjectChatListItemReadModel CreateListItem(
        Guid projectId,
        ProjectChatType chatType,
        ProjectChatStatus status)
    {
        return new ProjectChatListItemReadModel
        {
            ChatId = Guid.NewGuid(),
            ProjectId = projectId,
            ChatType = chatType,
            StaffId = Guid.NewGuid(),
            StaffName = "Project staff",
            Title = $"{chatType} chat",
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static ProjectChat CreateChat(
        Guid projectId,
        Guid staffId,
        ProjectChatStatus status)
    {
        return new ProjectChat
        {
            ChatId = Guid.NewGuid(),
            ProjectId = projectId,
            ChatType = ProjectChatType.SALES,
            StaffId = staffId,
            Title = "Original title",
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
    }

    private sealed class FakeProjectChatRepository : IProjectChatRepository
    {
        private readonly List<ProjectChat> _chats;
        private readonly ProjectChatAccessReadModel? _access;
        private readonly IReadOnlyList<ProjectChatListItemReadModel> _listItems;

        public FakeProjectChatRepository(
            IReadOnlyList<ProjectChat>? chats = null,
            IReadOnlyList<ProjectChatMessage>? messages = null,
            ProjectChatAccessReadModel? access = null,
            IReadOnlyList<ProjectChatListItemReadModel>? listItems = null)
        {
            _chats = chats?.ToList() ?? [];
            Messages = messages?.ToList() ?? [];
            _access = access;
            _listItems = listItems ?? [];
        }

        public IReadOnlyList<ProjectChat> Chats => _chats;
        public IReadOnlyList<ProjectChatMessage> Messages { get; }
        public int GetActiveCallCount { get; private set; }
        public int GetAccessCallCount { get; private set; }
        public int GetListCallCount { get; private set; }
        public int AddCallCount { get; private set; }
        public int UpdateCallCount { get; private set; }
        public int SaveChangesCallCount { get; private set; }
        public Guid LastListProjectId { get; private set; }
        public ProjectChatListQueryReadModel? LastListQuery { get; private set; }

        public Task<ProjectChatAccessReadModel?> GetAccessAsync(
            Guid projectId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            GetAccessCallCount++;
            return Task.FromResult(_access?.ProjectId == projectId ? _access : null);
        }

        public Task<(IReadOnlyList<ProjectChatListItemReadModel> Items, int Total)> GetListAsync(
            Guid projectId,
            ProjectChatListQueryReadModel query,
            CancellationToken cancellationToken = default)
        {
            GetListCallCount++;
            LastListProjectId = projectId;
            LastListQuery = query;

            IEnumerable<ProjectChatListItemReadModel> filtered = _listItems;
            filtered = query.Status.HasValue
                ? filtered.Where(chat => chat.Status == query.Status)
                : filtered.Where(chat => chat.Status != ProjectChatStatus.ARCHIVED);

            if (query.ChatType.HasValue)
            {
                filtered = filtered.Where(chat => chat.ChatType == query.ChatType);
            }

            if (query.AllowedChatTypes is not null)
            {
                filtered = filtered.Where(chat => query.AllowedChatTypes.Contains(chat.ChatType));
            }

            var visible = filtered.ToList();
            var page = visible.Skip((query.Page - 1) * query.Limit).Take(query.Limit).ToList();
            return Task.FromResult<(IReadOnlyList<ProjectChatListItemReadModel>, int)>((page, visible.Count));
        }

        public Task<ProjectChat?> GetActiveAsync(
            Guid projectId,
            ProjectChatType chatType,
            CancellationToken cancellationToken = default)
        {
            GetActiveCallCount++;
            return Task.FromResult(_chats.FirstOrDefault(chat =>
                chat.ProjectId == projectId &&
                chat.ChatType == chatType &&
                chat.Status != ProjectChatStatus.ARCHIVED));
        }

        public IQueryable<ProjectChat> Query() => _chats.AsQueryable();

        public Task<ProjectChat?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_chats.FirstOrDefault(chat => chat.ChatId == id));
        }

        public Task<IReadOnlyList<ProjectChat>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ProjectChat>>(_chats);
        }

        public Task AddAsync(ProjectChat entity, CancellationToken cancellationToken = default)
        {
            AddCallCount++;
            _chats.Add(entity);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<ProjectChat> entities, CancellationToken cancellationToken = default)
        {
            _chats.AddRange(entities);
            return Task.CompletedTask;
        }

        public void Update(ProjectChat entity)
        {
            UpdateCallCount++;
        }

        public void Remove(ProjectChat entity)
        {
            _chats.Remove(entity);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            return Task.FromResult(1);
        }
    }
}
